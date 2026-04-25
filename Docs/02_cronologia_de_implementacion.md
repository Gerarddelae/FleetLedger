# FleetLedger — Cronología de Implementación por Fases

## Visión General

```
Fase 1 (Días 1–4)   → Entidades de referencia: Driver y Depot con EF Core
Fase 2 (Días 5–9)   → Core del Event Sourcing: aggregate, invariantes, escritura
Fase 3 (Días 10–14) → Proyecciones y endpoints de lectura (CQRS completo)
Fase 4 (Días 15–18) → Auditoría avanzada y alertas predictivas
Fase 5 (Días 19+)   → Pulido de portfolio: tests, docs y demo
```

Se empieza por las entidades de referencia porque los eventos del vehículo dependen de ellas. Tener `Driver` y `Depot` funcionando primero permite validar esas referencias desde el primer evento que se emita.

---

## Fase 1 — Entidades de Referencia con EF Core (Días 1–4)

**Objetivo:** CRUD completo de `Driver` y `Depot` funcionando antes de tocar Event Sourcing.

### Día 1 — Setup del proyecto y estructura base

Crear la solución con todos los proyectos:

```bash
dotnet new sln -n FleetLedger
dotnet new webapi  -n FleetLedger.Api          -o src/FleetLedger.Api
dotnet new classlib -n FleetLedger.Domain      -o src/FleetLedger.Domain
dotnet new classlib -n FleetLedger.Application -o src/FleetLedger.Application
dotnet new classlib -n FleetLedger.Infrastructure -o src/FleetLedger.Infrastructure
dotnet new xunit   -n FleetLedger.Domain.Tests -o tests/FleetLedger.Domain.Tests
dotnet new xunit   -n FleetLedger.Integration.Tests -o tests/FleetLedger.Integration.Tests

# Agregar a la solución
dotnet sln add src/FleetLedger.Api
dotnet sln add src/FleetLedger.Domain
dotnet sln add src/FleetLedger.Application
dotnet sln add src/FleetLedger.Infrastructure
dotnet sln add tests/FleetLedger.Domain.Tests
dotnet sln add tests/FleetLedger.Integration.Tests

# Referencias entre proyectos (Clean Architecture)
# Api → Application → Domain
# Infrastructure → Application (implementa las interfaces) → Domain
dotnet add src/FleetLedger.Api reference src/FleetLedger.Application
dotnet add src/FleetLedger.Application reference src/FleetLedger.Domain
dotnet add src/FleetLedger.Infrastructure reference src/FleetLedger.Application
dotnet add src/FleetLedger.Infrastructure reference src/FleetLedger.Domain
```

Instalar paquetes iniciales:

```bash
# Infrastructure
dotnet add src/FleetLedger.Infrastructure package Microsoft.EntityFrameworkCore
dotnet add src/FleetLedger.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL

# Api
dotnet add src/FleetLedger.Api package Swashbuckle.AspNetCore --version 6.5.0
```

Configurar Swagger y `GET /health`. Crear `docker-compose.yml` con PostgreSQL.

**Nota de arquitectura:** La referencia `Infrastructure → Application` existe porque Infrastructure implementa las interfaces de repositorio (`IDriverRepository`, `IDepotRepository`, `IVehicleRepository`) definidas en Application. Api solo depende de Application (no ve a Infrastructure directamente), cumpliendo el principio de Inversión de Dependencias.

**Entregable:** El proyecto compila, conecta a PostgreSQL y responde `/health`.

---

### Día 2 — Entidad `Depot` con EF Core

Implementar `Depot` de extremo a extremo:

**Domain:**
```csharp
public class Depot
{
    public string Id { get; private set; }
    public string Name { get; private set; }
    public string Address { get; private set; }
    public string City { get; private set; }
    public string? Region { get; private set; }
    public string? ManagerName { get; private set; }
    public string? Phone { get; private set; }
    public bool Active { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
}
```

**Infrastructure:**
- `FleetLedgerDbContext` con `DbSet<Depot>`.
- `DepotConfiguration : IEntityTypeConfiguration<Depot>` con constraints (nombre único, índice).
- Primera migración de EF Core.

**Application:**
- `CreateDepotCommand` / `CreateDepotHandler`
- `UpdateDepotCommand` / `UpdateDepotHandler`
- `DeactivateDepotCommand` / `DeactivateDepotHandler`
- `GetDepotsQuery` / `GetDepotByIdQuery`

**Api:**
- `POST /depots`, `GET /depots`, `GET /depots/{id}`, `PUT /depots/{id}`, `DELETE /depots/{id}`.

**Reglas:**
- Nombre único → `409 Conflict` si se repite.
- `DELETE` hace soft delete (`Active = false`), nunca elimina físicamente.
- Si tiene vehículos asociados, igual permite desactivar (los vehículos mantienen su `DepotId` histórico).

**Entregable:** CRUD de depósitos funcionando y documentado en Swagger.

---

### Día 3 — Entidad `Driver` con EF Core

Implementar `Driver` con la misma estructura que `Depot`:

**Domain:**
```csharp
public class Driver
{
    public string Id { get; private set; }
    public string FullName { get; private set; }
    public string LicenseNumber { get; private set; }   // único
    public string LicenseCategory { get; private set; }
    public DateOnly LicenseExpires { get; private set; }
    public string? Phone { get; private set; }
    public string? DepotId { get; private set; }        // FK opcional a Depot
    public bool Active { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
}
```

**Infrastructure:**
- `DriverConfiguration : IEntityTypeConfiguration<Driver>`.
- FK opcional a `Depot` (un conductor puede no tener base fija).
- Índice único en `LicenseNumber`.
- Nueva migración.

**Application:**
- CRUD handlers para `Driver`.

**Api:**
- `POST /drivers`, `GET /drivers`, `GET /drivers/{id}`, `PUT /drivers/{id}`, `DELETE /drivers/{id}`.
- Filtros en `GET /drivers`: `?active=&depotId=&licenseCategory=`.

**Reglas:**
- Número de licencia único → `409 Conflict`.
- Si el `DepotId` se provee, debe existir y estar activo.
- `DELETE` hace soft delete. Si el conductor tiene asignaciones históricas en algún vehículo, igual se permite desactivar.

**Entregable:** CRUD de conductores funcionando. La relación `Driver → Depot` es navegable.

---

### Día 4 — Validación cruzada y refinamiento del CRUD

- Agregar validaciones de input con FluentValidation o DataAnnotations en los requests de `Driver` y `Depot`.
- Tests de integración para el CRUD: crear, leer, actualizar, soft delete, intentar duplicar nombre/licencia.
- Documentar en Swagger los casos de error de cada endpoint (`409`, `404`, `422`).
- Verificar que `GET /drivers?depotId=DEP-001` retorna solo conductores de ese depósito.

**Entregable:** Entidades de referencia completas, testeadas y documentadas. Base lista para que los eventos de vehículos las referencien.

---

## Fase 2 — Core del Event Sourcing (Días 5–9)

**Objetivo:** El aggregate `Vehicle` puede recibir eventos, validar invariantes (incluyendo referencias a `Driver` y `Depot`) y persistirlos en Marten.

### Día 5 — Setup de Marten y VehicleState

Instalar Marten:
```bash
dotnet add src/FleetLedger.Infrastructure package Marten
```

Configurar Marten en `Program.cs` junto a EF Core:
```csharp
// Ambos apuntan a la misma PostgreSQL pero son independientes
builder.Services.AddDbContext<FleetLedgerDbContext>(opts =>
    opts.UseNpgsql(connectionString));

builder.Services.AddMarten(opts => {
    opts.Connection(connectionString);
    opts.Events.StreamIdentity = StreamIdentity.AsString;
}).UseLightweightSessions();
```

Definir todos los records de eventos del dominio (15 eventos). Implementar `VehicleState` como record inmutable. Implementar `Vehicle` aggregate con todos los `Apply(event)`.

**Entregable:** El aggregate reconstruye su estado desde una secuencia de eventos. Testeado con unit tests sin base de datos.

---

### Día 6 — Invariantes del Aggregate (puras)

Implementar y testear las invariantes que el aggregate puede validar solo, sin consultar la base de datos:

- `TripStarted`: 6 precondiciones.
- `TripCompleted`, `TripCancelled`.
- `BreakdownReported`, `BreakdownResolved`.
- `OdometerAdjusted` (incluyendo `approvedBy` para corrección regresiva).
- `InspectionPassed`, `InspectionFailed`.
- `DriverAssigned`, `DriverUnassigned`.
- `VehicleDecommissioned`.
- Invariantes transversales: timestamp no regresivo, vehículo no dado de baja.

**Entregable:** Suite de unit tests con caso válido y caso de violación por cada invariante. Sin base de datos, sin Marten.

---

### Día 7 — Validaciones cruzadas con entidades de referencia

Implementar las validaciones que requieren consultar EF Core, en el **Application layer**:

```csharp
public class RecordEventHandler
{
    private readonly IVehicleRepository _vehicleRepo;      // Marten
    private readonly IDriverRepository _driverRepo;        // EF Core
    private readonly IDepotRepository _depotRepo;          // EF Core

    public async Task Handle(RecordVehicleEventCommand cmd, CancellationToken ct)
    {
        // Validaciones cruzadas antes de tocar el aggregate
        if (cmd.Type == "DriverAssigned")
        {
            var driver = await _driverRepo.FindAsync(cmd.DriverId, ct)
                ?? throw new DriverNotFoundException(cmd.DriverId);
            if (!driver.Active)
                throw new DriverInactiveException(cmd.DriverId);
        }

        // Luego cargar el aggregate y validar invariantes de dominio
        var vehicle = await _vehicleRepo.LoadAsync(cmd.VehicleId, ct)
            ?? throw new VehicleNotFoundException(cmd.VehicleId);

        var @event = vehicle.Handle(cmd);
        await _vehicleRepo.AppendAsync(cmd.VehicleId, @event, ct);
    }
}
```

Lo mismo para `VehicleAcquired` con `DepotId`.

**Entregable:** Los eventos que referencian `Driver` o `Depot` validan que existan y estén activos antes de persistirse.

---

### Día 8 — Endpoints de escritura y middleware de errores

- `POST /vehicles` → `VehicleAcquired`.
- `POST /vehicles/{id}/events` → DTO polimórfico con campo `type`.
- `DomainExceptionHandler` middleware que mapea todas las excepciones de dominio a HTTP con mensajes descriptivos.
- Documentar en Swagger con ejemplos de cada tipo de evento y sus posibles errores.

**Entregable:** Se puede crear vehículos y registrar cualquier evento desde Swagger. Los errores son descriptivos.

---

### Día 9 — Tests de integración de escritura

- Tests end-to-end con PostgreSQL real (Testcontainers).
- Escenario: crear depósito → crear conductor → crear vehículo → registrar eventos → verificar que las invariantes se respetan en el stack completo.
- Verificar que un conductor inactivo es rechazado en `DriverAssigned`.
- Verificar que un depósito inactivo es rechazado en `VehicleAcquired`.

**Entregable:** Los endpoints de escritura tienen cobertura de integración completa.

---

## Fase 3 — Proyecciones y Endpoints de Lectura (Días 10–14)

**Objetivo:** CQRS completo. Las lecturas sirven proyecciones precalculadas.

### Día 10 — Proyección de Estado Actual

- `VehicleStatusProjection` con Marten async projections.
- `GET /vehicles/{id}/status`.
- `GET /fleet/status` con filtros `?status=&depotId=&hasAlerts=true`.
- El filtro por `depotId` cruza la proyección de estado con el `DepotId` almacenado en el evento `VehicleAcquired`.

**Entregable:** Estado de flota filtrable por depósito, desde proyección, sin tocar el event store.

---

### Día 11 — Timeline de Eventos

- `GET /vehicles/{id}/timeline` con filtros `?from=&to=&type=` y paginación.
- Lee el event store directamente (excepción documentada en el README).

**Entregable:** Historial completo y filtrado de cualquier vehículo.

---

### Día 12 — Proyección de Compliance

- `ComplianceProjection` escuchando `InspectionPassed` e `InspectionFailed`.
- `GET /fleet/compliance?type=&expiresWithinDays=&status=failed|expired|upcoming`.
- Incluye el `DepotId` del vehículo para permitir filtrado por depósito.

**Entregable:** Vista completa del estado legal/técnico de la flota, filtrable por depósito.

---

### Día 13 — Proyección de Costos

- `CostProjection` escuchando `MaintenancePerformed`, `FineIssued`, `TripCompleted`.
- `GET /vehicles/{id}/costs?from=&to=`.
- `GET /fleet/costs?depotId=&from=&to=` — costos agregados por depósito.

**Entregable:** Análisis financiero filtrable por período y por depósito.

---

### Día 14 — Salud de proyecciones y cierre del MVP

- `ProjectionCheckpointProjection` registra último evento procesado por proyección.
- `GET /projections/health` con lag y estado `healthy/degraded`.
- Umbral configurable en `appsettings.json`.
- Verificar idempotencia de todas las proyecciones.

**Entregable:** MVP completo. Demo end-to-end demostrable desde Swagger.

---

## Fase 4 — Auditoría Avanzada y Alertas (Días 15–18)

### Día 15 — Auditoría Point-in-Time

- `GET /vehicles/{id}/state-at?date=`.
- Reproduce el stream hasta el timestamp. Devuelve `DriverId` asignado en ese momento (el cliente puede resolver el nombre con `GET /drivers/{id}`).

**Entregable:** El endpoint de auditoría más poderoso del sistema.

---

### Día 16 — Alertas de Mantenimiento Predictivo

- Reglas configurables en `appsettings.json` por tipo de mantenimiento.
- `MaintenanceAlertsProjection` evalúa reglas al recibir `TripCompleted`, `MaintenancePerformed`, `OdometerAdjusted`.
- `GET /maintenance/alerts?status=overdue|upcoming&vehicleId=&depotId=`.

**Entregable:** Alertas preventivas filtrables por depósito.

---

### Día 17 — Escenario de demo end-to-end

Seed que genera historia completa:

```
POST /depots          → DEP-001 "Buenos Aires Norte"
POST /drivers         → DRV-009 "Carlos Méndez", DRV-014 "Luis Torres"
POST /vehicles        → demo-truck-01, depotId: DEP-001

POST /vehicles/demo-truck-01/events → DriverAssigned (DRV-009)
POST /vehicles/demo-truck-01/events → TripStarted (km: 0)
POST /vehicles/demo-truck-01/events → TripCompleted (km: 520)
POST /vehicles/demo-truck-01/events → InspectionPassed (nextDue: 2026-04-15)
POST /vehicles/demo-truck-01/events → MaintenancePerformed (OilChange, km: 5000)
POST /vehicles/demo-truck-01/events → TripStarted (km: 5000)
POST /vehicles/demo-truck-01/events → BreakdownReported (brakes, km: 5200)
  → intentar TripStarted aquí → 422 ActiveBreakdownException ✓
POST /vehicles/demo-truck-01/events → BreakdownResolved
  → sin MaintenancePerformed previo — igual funciona ✓
POST /vehicles/demo-truck-01/events → TripStarted (km: 5200)
POST /vehicles/demo-truck-01/events → TripCompleted (km: 6800)
POST /vehicles/demo-truck-01/events → FineIssued (DRV-009, speeding, $150)
POST /vehicles/demo-truck-01/events → DriverUnassigned
POST /vehicles/demo-truck-01/events → DriverAssigned (DRV-014)
```

Preguntas que el sistema responde:
```
GET /vehicles/demo-truck-01/state-at?date=2026-01-10T15:00
→ HasActiveBreakdown: true, AssignedDriverId: DRV-009

GET /drivers/DRV-009
→ FullName: "Carlos Méndez" (resolución del nombre)

GET /fleet/status?depotId=DEP-001
→ demo-truck-01 aparece en el resultado

GET /maintenance/alerts?depotId=DEP-001
→ BrakeService próximo (nunca se realizó)
```

**Entregable:** Demo reproducible que cuenta una historia convincente.

---

### Día 18 — Revisión y refinamiento

- Mensajes de error descriptivos en todos los endpoints.
- Ejemplos en Swagger para todos los endpoints incluyendo CRUD de Driver y Depot.
- Documentar en el README la decisión de usar EF Core para entidades de referencia y Marten para el event store.

**Entregable:** API sin rough edges, lista para mostrar.

---

## Fase 5 — Pulido de Portfolio (Días 19+)

### Tests

Nombres de test que documentan el comportamiento:
- `DriverAssigned_WithInactiveDriver_Throws`
- `VehicleAcquired_WithInactiveDepot_Throws`
- `BreakdownResolved_DoesNotRequireMaintenancePerformed`
- `StateAt_ReturnsCorrectDriverBeforeUnassignment`
- `FleetStatus_FilteredByDepot_ReturnsOnlyDepotVehicles`

### Docker

```yaml
# docker-compose.yml
services:
  db:
    image: postgres:16
    environment:
      POSTGRES_DB: fleetledger
      POSTGRES_USER: fleet
      POSTGRES_PASSWORD: fleet
    ports: ["5432:5432"]

  api:
    build: .
    environment:
      ConnectionStrings__Default: "Host=db;Database=fleetledger;Username=fleet;Password=fleet"
    ports: ["8080:8080"]
    depends_on: [db]
```

### README — Sección de decisiones arquitectónicas

Debe incluir:
- Por qué Event Sourcing para vehículos (argumento de la auditoría legal).
- Por qué CRUD con EF Core para Driver y Depot (entidades de referencia estables, no se auditan en el tiempo).
- Por qué Marten y EF Core pueden coexistir sin problema.
- Cómo levantar el proyecto con `docker compose up`.
- El escenario de demo con las preguntas que responde.

---

## Checklist de MVP Completo (al finalizar Fase 4)

**CRUD de referencia:**
- [ ] Puedo crear, editar y desactivar conductores y depósitos.
- [ ] Un conductor inactivo es rechazado al intentar asignarlo.
- [ ] Un depósito inactivo es rechazado al crear un vehículo.

**Event Sourcing:**
- [ ] Puedo registrar el ciclo de vida completo de un vehículo.
- [ ] Las invariantes del aggregate son respetadas con errores descriptivos.
- [ ] Las referencias a Driver y Depot son validadas antes de persistir eventos.

**Lecturas:**
- [ ] Estado actual de la flota filtrable por depósito.
- [ ] Costos operativos por vehículo y por depósito.
- [ ] Compliance con inspecciones vencidas o fallidas.
- [ ] Alertas de mantenimiento predictivo.
- [ ] Estado exacto de un vehículo en cualquier momento del pasado.
- [ ] Salud de las proyecciones expuesta en un endpoint.
