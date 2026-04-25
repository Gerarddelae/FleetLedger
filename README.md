# FleetLedger - API de gestion de flotas vehiculares

<img width="1536" height="1024" alt="image" src="https://github.com/user-attachments/assets/d07d6bef-0149-4e48-a41b-bdb5b9832980" />

API REST de gestión de flotas vehiculares con **Event Sourcing + CQRS** para vehículos y **CRUD tradicional con EF Core** para entidades de referencia.

## ¿Por qué Event Sourcing para vehículos?

Los vehículos son activos con **historial legal y operativo complejo**. Cada evento (viaje, mantenimiento, multa, inspección) es un hecho inmutable que debe mantenerse para:

- **Auditorías legales**: reconstruir el estado exacto de un vehículo en cualquier momento del pasado
- **Trazabilidad**: saber quién conducía cada vehículo en cada momento
- **Cumplimiento**: verificar ITV, inspecciones y licencias en cualquier fecha histórica
- **Análisis**: calcular costos operativos, predictivos y de mantenimiento

Un vehículo no es solo "datos actuales" — es una **secuencia de hechos** que define su historia completa.

## Arquitectura de Dependencias

```
                    ┌─────────────────┐
                    │  FleetLedger.Api │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │ FleetLedger.Application │ ← Define interfaces (puertos)
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │ FleetLedger.Infrastructure │ ← Implementa las interfaces
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │  FleetLedger.Domain  │
                    └─────────────────────┘
```

**Principio de Inversión de Dependencias:**
- `Application` define los puertos (`IDriverRepository`, `IDepotRepository`, `IVehicleRepository`)
- `Infrastructure` implementa esos puertos
- `Api` solo conoce `Application`, nunca referencia a `Infrastructure` directamente

## Decisiones de Diseño

### ¿Por qué Event Sourcing + CQRS para vehículos?

- **Auditoría Point-in-Time**: capacidad de reconstruir el estado exacto en cualquier momento del pasado (ej: disputas legales)
- **Inmutabilidad**: ningún evento se modifica; `OdometerAdjusted` es el único mecanismo de corrección con auditoría
- **Trazabilidad completa**: cada cambio queda registrado con timestamp, permitiendo responder "qué pasaba en tal fecha"
- **Proyecciones optimizadas**: lecturas desde proyecciones precalculadas, no desde el event store

### ¿Por qué CRUD con EF Core para Driver y Depot?

Los conductores y depósitos son **entidades de referencia estables**:
- No interesa auditar "cuándo cambió el teléfono de Juan Pérez"
- Lo que sí interesa es cuándo Juan Pérez manejó cada vehículo → eso queda en el stream del vehículo via `DriverAssigned`
- Son datos que se consultan frecuentemente pero no requieren historial de cambios
- CRUD tradicional es más simple y suficiente para este caso

### ¿Por qué Marten + EF Core coexistiendo?

- Ambos apuntan a la **misma PostgreSQL** pero son independientes
- **Marten** gestiona sus propias tablas (`mt_events`, `mt_streams`, tablas de proyecciones)
- **EF Core** gestiona las tablas relacionales (`Drivers`, `Depots`)
- La integridad referencial es **suave**: el Application layer valida que los IDs existan antes de persistir eventos

## Stack Tecnológico

| Componente | Tecnología |
|------------|------------|
| Runtime | .NET 10.0 |
| Web Framework | ASP.NET Core (Minimal APIs) |
| Event Store | Marten (PostgreSQL) |
| ORM | Entity Framework Core + Npgsql |
| Documentación | Swagger/OpenAPI |
| Testing | xUnit |

## Estructura del Proyecto

```
FleetLedger/
├── src/
│   ├── FleetLedger.Api/          # Minimal APIs, endpoints
│   ├── FleetLedger.Domain/        # Entidades, eventos del dominio, excepciones
│   ├── FleetLedger.Application/   # Commands, queries, interfaces de repositorio
│   └── FleetLedger.Infrastructure/# Implementaciones, EF Core, Marten, proyecciones
├── tests/
│   ├── FleetLedger.Domain.Tests/        # Tests unitarios del agregado
│   └── FleetLedger.Integration.Tests/   # Tests de integración
├── docker-compose.yml
└── README.md
```

## Cómo Levantar el Proyecto

### Prerrequisitos

- .NET 10.0 SDK
- Docker (para PostgreSQL)

### Pasos

1. **Clonar el repositorio**

2. **Iniciar PostgreSQL:**
   ```bash
   docker compose up -d db
   ```

3. **Ejecutar la API:**
   ```bash
   dotnet run --project src/FleetLedger.Api
   ```

4. **Verificar que funciona:**
   - Health check: `http://localhost:5000/health`
   - Swagger UI: `http://localhost:5000/`

### Para desarrollo con hot-reload

```bash
dotnet watch --project src/FleetLedger.Api
```

## Endpoints Principales

### Depósitos (CRUD - EF Core)
```
POST   /depots              → crear depósito
GET    /depots              → listar (filtros: ?active=&region=)
GET    /depots/{id}         → obtener por ID
PUT    /depots/{id}         → actualizar
DELETE /depots/{id}         → soft delete
```

### Conductores (CRUD - EF Core)
```
POST   /drivers              → crear conductor
GET    /drivers              → listar (filtros: ?active=&depotId=&licenseCategory=)
GET    /drivers/{id}         → obtener por ID
PUT    /drivers/{id}         → actualizar
DELETE /drivers/{id}         → soft delete
```

### Vehículos (Event Sourcing - Marten)
```
POST   /vehicles                    → VehicleAcquired (crear vehículo)
POST   /vehicles/{id}/events       → registrar evento
GET    /vehicles/{id}/status       → estado actual (proyección)
GET    /vehicles/{id}/timeline     → historial de eventos
GET    /vehicles/{id}/state-at     → estado en un momento del pasado
```

### Flota
```
GET /fleet/status       → estado de la flota (?depotId=&status=)
GET /fleet/compliance   → vehículos con inspecciones vencidas/fallidas
GET /fleet/costs        → costos operativos (?depotId=&from=&to=)
```

## Reglas de Negocio Clave

- **Soft delete**: `DELETE` en Driver/Depot no elimina físicamente, solo establece `Active = false`
- **Validación cruzada**: 
  - `DriverAssigned` requiere que el conductor exista y esté activo
  - `VehicleAcquired` requiere que el depósito exista y esté activo
- **Invariantes del vehículo**: 
  - No se puede iniciar viaje con avería activa, ITV vencida o inspección fallida
  - Odómetro nunca puede decrease (corrección requiere `approvedBy`)

## Roadmap de Implementación

| Fase | Descripción |
|------|-------------|
| Día 1 | Setup del proyecto, Swagger, Health endpoint |
| Día 2 | CRUD Depot con EF Core |
| Día 3 | CRUD Driver con EF Core |
| Días 5-9 | Event Sourcing: aggregate, invariantes, eventos |
| Días 10-14 | Proyecciones y endpoints de lectura (CQRS) |
| Días 15-18 | Auditoría point-in-time, alertas predictivas |

## Licencia

MIT
