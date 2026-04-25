# FleetLedger — Especificaciones, Requerimientos e Historias de Usuario

## Descripción del Proyecto

FleetLedger es una API REST de gestión de flotas vehiculares basada en **Event Sourcing + CQRS** para el dominio principal (vehículos), complementado con **CRUD tradicional sobre EF Core** para entidades de referencia estables (conductores y depósitos). Cada vehículo es un stream de eventos inmutable. Los conductores y depósitos son datos de referencia que se consultan pero no se auditan en el tiempo.

Esta convivencia de patrones es una decisión de diseño consciente: no todo en un sistema necesita Event Sourcing. Los activos con historial legal y operativo complejo lo justifican; las entidades de referencia estables no.

---

## Modelo de Dominio — Tres Contextos

```
┌─────────────────────────────────────────────────────────────┐
│  Vehicles (Event Sourcing + CQRS)                           │
│  Fuente de verdad: stream de eventos inmutables             │
│  Lectura: proyecciones precalculadas                        │
├─────────────────────────────────────────────────────────────┤
│  Drivers (CRUD con EF Core)                                 │
│  Fuente de verdad: tabla relacional                         │
│  Propósito: entidad de referencia para asignaciones         │
├─────────────────────────────────────────────────────────────┤
│  Depots (CRUD con EF Core)                                  │
│  Fuente de verdad: tabla relacional                         │
│  Propósito: entidad de referencia para agrupación y filtros │
└─────────────────────────────────────────────────────────────┘
```

Los vehículos referencian conductores y depósitos por ID. FleetLedger valida que esos IDs existan antes de emitir eventos que los involucren, pero no duplica sus datos dentro del event store.

---

## Entidades de Referencia (EF Core)

### Driver — Conductor

Representa a la persona habilitada para operar vehículos de la flota.

**Campos:**
```
Id              : string (generado, ej: DRV-001)
FullName        : string (requerido)
LicenseNumber   : string (requerido, único)
LicenseCategory : string (ej: "C", "D", "E")
LicenseExpires  : DateOnly (requerido)
Phone           : string? (opcional)
DepotId         : string? (FK a Depot, opcional — conductor puede no tener base fija)
Active          : bool (default: true — soft delete)
CreatedAt       : DateTime
UpdatedAt       : DateTime
```

**Reglas de negocio:**
- El número de licencia debe ser único en el sistema.
- Un conductor con `Active = false` no puede ser asignado a un vehículo (validado antes de emitir `DriverAssigned`).
- No se elimina físicamente un conductor si tiene eventos históricos en algún stream de vehículo. Solo se desactiva.

**Por qué no Event Sourcing:** los conductores son datos de referencia. No interesa auditar "cuándo cambió el teléfono de Juan Pérez". Lo que sí interesa auditar es cuándo Juan Pérez manejó cada vehículo, y eso ya queda en el stream del vehículo via `DriverAssigned`.

---

### Depot — Depósito / Base operativa

Representa la ubicación física donde los vehículos tienen su base.

**Campos:**
```
Id          : string (generado, ej: DEP-001)
Name        : string (requerido, único)
Address     : string (requerido)
City        : string (requerido)
Region      : string? (opcional — para agrupaciones geográficas)
ManagerName : string? (opcional)
Phone       : string? (opcional)
Active      : bool (default: true — soft delete)
CreatedAt   : DateTime
UpdatedAt   : DateTime
```

**Reglas de negocio:**
- El nombre del depósito debe ser único.
- Un depósito con `Active = false` no puede ser asignado como base de un nuevo vehículo.
- No se elimina físicamente si tiene vehículos asociados.

**Por qué importa como entidad:** sin `Depot` como entidad, los filtros de flota por base (`GET /fleet/status?depotId=DEP-001`) son un string match sin validación. Con la entidad, se puede agrupar vehículos por región, calcular costos por depósito y filtrar compliance por zona geográfica.

---

## Endpoints de las Entidades de Referencia

### Drivers

```
POST   /drivers              → crear conductor
GET    /drivers              → listar conductores (filtros: ?active=&depotId=&licenseCategory=)
GET    /drivers/{id}         → obtener conductor por ID
PUT    /drivers/{id}         → actualizar datos del conductor
DELETE /drivers/{id}         → soft delete (Active = false)
```

### Depots

```
POST   /depots               → crear depósito
GET    /depots               → listar depósitos (filtros: ?active=&region=)
GET    /depots/{id}          → obtener depósito por ID
PUT    /depots/{id}          → actualizar datos del depósito
DELETE /depots/{id}          → soft delete (Active = false)
```

---

## Eventos del Dominio (Vehículos)

Cada evento es un hecho ocurrido, inmutable. El aggregate `Vehicle` valida las invariantes **antes** de emitirlo.

### Ciclo de vida del activo

| Evento | Intención |
|---|---|
| `VehicleAcquired` | El vehículo ingresa a la flota, referencia `DepotId` |
| `VehicleDecommissioned` | El vehículo es dado de baja definitivamente |

### Operación

| Evento | Intención |
|---|---|
| `DriverAssigned` | Se asigna un conductor (referencia `DriverId`) |
| `DriverUnassigned` | Se desvincula explícitamente al conductor actual |
| `TripStarted` | Inicia un viaje con odómetro y ruta |
| `TripCompleted` | Finaliza el viaje con odómetro y combustible |
| `TripCancelled` | Cancela un viaje iniciado antes de completarse |

### Mantenimiento e incidencias

| Evento | Intención |
|---|---|
| `BreakdownReported` | Se registra una avería activa |
| `BreakdownResolved` | Se cierra explícitamente la avería activa |
| `MaintenancePerformed` | Se realiza un mantenimiento (independiente de averías) |
| `OdometerAdjusted` | Corrección auditada del odómetro con motivo obligatorio |

### Compliance y legal

| Evento | Intención |
|---|---|
| `InspectionPassed` | Inspección aprobada con fecha de próximo vencimiento |
| `InspectionFailed` | Inspección fallida; bloquea operación hasta nueva aprobación |
| `FineIssued` | Multa registrada (referencia `DriverId` del conductor infractor) |

---

## Invariantes del Aggregate `Vehicle`

### Invariantes transversales

- **Timestamp no regresivo:** ningún evento puede tener fecha anterior al último evento del stream.
- **Vehículo existente:** todos los eventos requieren stream iniciado por `VehicleAcquired`.
- **Vehículo no dado de baja:** ningún evento operativo sobre vehículo `Decommissioned`.

### Validaciones que cruzan a las entidades de referencia

Estas validaciones ocurren en el **Application layer**, antes de pasar al aggregate, porque requieren consultar la base de datos relacional:

- **`DriverAssigned`:** el `DriverId` debe existir y tener `Active = true`.
- **`VehicleAcquired`:** el `DepotId` debe existir y tener `Active = true`.
- **`FineIssued`:** el `DriverId` debe existir (puede estar inactivo — una multa histórica sigue siendo válida).

### Por evento (invariantes del aggregate puro)

#### `VehicleAcquired`
- El `VehicleId` no debe tener stream previo.
- El formato de patente debe ser válido.
- El precio de compra debe ser `>= 0`.

#### `DriverAssigned`
- No debe haber conductor asignado actualmente (requiere `DriverUnassigned` previo).
- `DriverId` no puede estar vacío.

#### `DriverUnassigned`
- Debe existir conductor asignado actualmente.

#### `TripStarted`
- No debe haber viaje en curso.
- No debe haber avería activa.
- No debe haber inspección fallida sin `InspectionPassed` posterior.
- La ITV no debe estar vencida.
- El odómetro debe ser `>= odómetro actual`.
- Debe haber conductor asignado.

#### `TripCompleted`
- Debe haber viaje en curso.
- Odómetro final debe ser `> odómetro al iniciar el viaje`.
- Litros de combustible deben ser `>= 0`.

#### `TripCancelled`
- Debe haber viaje en curso.
- Campo `reason` obligatorio.

#### `BreakdownReported`
- No debe haber avería activa previa.
- Severidad debe ser valor válido del enum.
- Odómetro informado debe ser `>= odómetro actual`.

#### `BreakdownResolved`
- Debe existir avería activa.
- Independiente de `MaintenancePerformed`.

#### `MaintenancePerformed`
- Costo `>= 0`.
- Tipo de mantenimiento válido.
- Odómetro `>= odómetro actual`.
- No resuelve averías implícitamente.

#### `OdometerAdjusted`
- Campo `reason` obligatorio.
- Nuevo valor `>= 0`.
- Si nuevo valor es menor al actual, requiere campo `approvedBy`.
- No puede emitirse con viaje en curso.

#### `InspectionPassed`
- `nextDue` debe ser fecha posterior al evento.
- Desbloquea `HasFailedInspection`.

#### `InspectionFailed`
- Lista de deficiencias no puede estar vacía.
- Activa bloqueo operativo hasta `InspectionPassed` posterior.

#### `FineIssued`
- Monto `> 0`.
- Motivo no vacío.

#### `VehicleDecommissioned`
- Motivo no vacío.
- Sin viaje en curso.
- Sin avería activa sin resolver.

---

## Estado Interno del Aggregate (`VehicleState`)

Siempre derivado del stream, nunca persistido directamente.

```
Odometer              : int
Status                : Available | InTransit | Decommissioned
HasActiveBreakdown    : bool
TripInProgress        : bool
TripStartOdometer     : int?
AssignedDriverId      : string?
InspectionNextDue     : DateTime?
HasFailedInspection   : bool
LastEventTimestamp    : DateTime
```

---

## Proyecciones (Read Models — Vehículos)

| Proyección | Eventos que escucha | Propósito |
|---|---|---|
| `VehicleStatusProjection` | Todos los eventos operativos | Estado actual del vehículo |
| `MaintenanceAlertsProjection` | `MaintenancePerformed`, `TripCompleted`, `OdometerAdjusted` | Alertas predictivas por km/días |
| `ComplianceProjection` | `InspectionPassed`, `InspectionFailed` | Vencimientos y bloqueos |
| `CostProjection` | `MaintenancePerformed`, `FineIssued`, `TripCompleted` | Costos acumulados |

---

## Requerimientos Funcionales

### Vehículos (Event Sourcing)

**RF-01 — Registro de Vehículos:** incorporar vehículo con `VehicleAcquired`. Requiere `DepotId` válido y activo.

**RF-02 — Registro de Eventos de Vida:** aceptar todos los eventos del dominio vía `POST /vehicles/{id}/events`. Inmutables una vez persistidos. Violación de invariante → `422` con descripción del motivo específico.

**RF-03 — Consulta de Estado Actual:** estado operativo desde proyección precalculada.

**RF-04 — Timeline de Eventos:** secuencia cronológica con filtros por fecha y tipo.

**RF-05 — Auditoría Point-in-Time:** reconstrucción del estado en cualquier momento del pasado.

**RF-06 — Alertas de Mantenimiento Predictivo:** alertas por km y días con reglas configurables.

**RF-07 — Compliance y Vencimientos:** vehículos con inspecciones vencidas, fallidas o próximas a vencer.

**RF-08 — Costos Operativos:** costos acumulados por período, por vehículo y por flota.

**RF-09 — Salud de Proyecciones:** lag y estado de cada proyección.

### Conductores (CRUD — EF Core)

**RF-10 — Gestión de Conductores:** CRUD completo. Soft delete. Validación de licencia única. Un conductor inactivo no puede asignarse a un vehículo.

**RF-11 — Consulta de Conductores por Depósito:** listar conductores filtrando por `depotId`, `active`, `licenseCategory`.

### Depósitos (CRUD — EF Core)

**RF-12 — Gestión de Depósitos:** CRUD completo. Soft delete. Nombre único. Un depósito inactivo no puede asignarse a un vehículo nuevo.

**RF-13 — Consulta de Vehículos por Depósito:** `GET /fleet/status?depotId=` filtra la proyección de estado de flota por depósito base.

---

## Requerimientos No Funcionales

**RNF-01 — Inmutabilidad del Event Store:** ningún evento puede modificarse. `OdometerAdjusted` es el mecanismo de corrección.

**RNF-02 — Separación comando/query:** escrituras sobre aggregate; lecturas sobre proyecciones. Excepciones documentadas: `/timeline` y `/state-at`.

**RNF-03 — Consistencia eventual:** proyecciones con lag < 2s. Expuesto en `/projections/health`.

**RNF-04 — Idempotencia de proyecciones:** reconstruibles desde cero sin corrupción.

**RNF-05 — Integridad referencial suave:** FleetLedger valida que `DriverId` y `DepotId` existan antes de emitir eventos que los referencian. No usa foreign keys entre el event store y las tablas relacionales.

**RNF-06 — Documentación de API:** Swagger/OpenAPI con ejemplos de request, response y errores para todos los endpoints.

---

## Historias de Usuario

### HU-01 — Gestionar conductores
**Como** administrador de flota,
**quiero** registrar, actualizar y desactivar conductores,
**para** tener un directorio de personas habilitadas para operar los vehículos.

**Criterios de aceptación:**
- `POST /drivers` crea el conductor y retorna el ID generado.
- Número de licencia duplicado → `409 Conflict`.
- `DELETE /drivers/{id}` hace soft delete; si el conductor tiene asignaciones históricas, no falla.
- Un conductor inactivo no puede asignarse a un vehículo → `422` al intentar `DriverAssigned`.

---

### HU-02 — Gestionar depósitos
**Como** administrador de flota,
**quiero** registrar y mantener los depósitos de la flota,
**para** poder agrupar y filtrar vehículos por base operativa.

**Criterios de aceptación:**
- `POST /depots` crea el depósito y retorna el ID generado.
- Nombre duplicado → `409 Conflict`.
- `DELETE /depots/{id}` hace soft delete; si tiene vehículos asociados, no falla pero el depósito queda inactivo.
- Un depósito inactivo no puede usarse en `VehicleAcquired` → `422`.

---

### HU-03 — Incorporar un vehículo a la flota
**Como** operador de flota,
**quiero** registrar un nuevo vehículo con su depósito base,
**para** comenzar a registrar su historial desde la adquisición.

**Criterios de aceptación:**
- `POST /vehicles` requiere `depotId` válido y activo.
- VIN duplicado → `409 Conflict`.
- `DepotId` inexistente o inactivo → `422`.

---

### HU-04 — Asignar conductor a vehículo
**Como** despachador,
**quiero** asignar y desasignar conductores de forma explícita,
**para** tener trazabilidad exacta de quién manejaba cada vehículo.

**Criterios de aceptación:**
- `DriverAssigned` valida que el conductor exista y esté activo antes de emitir el evento.
- Conductor ya asignado → `422` (requiere `DriverUnassigned` previo).
- Conductor inactivo → `422` con mensaje "Driver {id} is inactive".

---

### HU-05 — Filtrar flota por depósito
**Como** supervisor regional,
**quiero** ver el estado de los vehículos de mi depósito,
**para** gestionar solo los activos bajo mi responsabilidad.

**Criterios de aceptación:**
- `GET /fleet/status?depotId=DEP-001` retorna solo vehículos con ese depósito base.
- `GET /fleet/costs?depotId=DEP-001&from=&to=` retorna costos agregados del depósito.
- Si el `depotId` no existe → `404`.

---

### HU-06 — Registrar un viaje completo
**Como** sistema de despacho,
**quiero** registrar inicio y fin de viaje,
**para** mantener el odómetro actualizado y calcular consumo.

**Criterios de aceptación:**
- `TripStarted` rechazado si: avería activa, inspección fallida, ITV vencida, viaje en curso, sin conductor asignado, odómetro regresivo.
- `TripCompleted` rechazado si: sin viaje en curso, odómetro final ≤ inicial.
- `TripCancelled` cierra el viaje; requiere `reason`.

---

### HU-07 — Gestionar averías y resolución explícita
**Como** conductor y jefe de taller,
**quiero** reportar y cerrar averías de forma separada del mantenimiento,
**para** reflejar con precisión el período real de inoperatividad.

**Criterios de aceptación:**
- `BreakdownReported` bloquea `TripStarted`.
- `BreakdownResolved` levanta el bloqueo, independiente de `MaintenancePerformed`.
- `MaintenancePerformed` no resuelve averías automáticamente.

---

### HU-08 — Corregir odómetro con auditoría
**Como** administrador,
**quiero** corregir errores de carga del odómetro,
**para** que los cálculos de mantenimiento sean precisos sin corromper el historial.

**Criterios de aceptación:**
- `OdometerAdjusted` requiere `reason` obligatorio.
- Corrección hacia atrás requiere `approvedBy`.
- Con viaje en curso → `422`.

---

### HU-09 — Reconstruir estado en el pasado
**Como** abogado en contexto de disputa legal,
**quiero** conocer el estado exacto de un vehículo en un momento preciso,
**para** determinar conductor, averías activas y vigencia de documentación.

**Criterios de aceptación:**
- `GET /vehicles/{id}/state-at?date=` reproduce el stream hasta ese timestamp.
- Respuesta incluye: odómetro, conductor, estado, avería activa, vigencia ITV, inspección fallida activa.
- Vehículo inexistente a esa fecha → `404`.

---

### HU-10 — Consultar salud de proyecciones
**Como** operador del sistema,
**quiero** monitorear el lag de las proyecciones,
**para** detectar degradación antes de que afecte a los usuarios.

**Criterios de aceptación:**
- `GET /projections/health` devuelve nombre, último evento procesado, lag en segundos y estado `healthy/degraded`.
- Umbral de degradación configurable en `appsettings.json`.

---

## Fuera de Scope (MVP)

- GPS o tracking en tiempo real.
- Gestión de rutas como entidad (se usa `routeId` como referencia libre).
- Integración con proveedores de combustible.
- Autenticación y autorización.
- Frontend o dashboard visual.
- Notificaciones push o webhooks.
