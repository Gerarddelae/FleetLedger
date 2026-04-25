# FleetLedger — Estructura de Directorios e Implementación

## Estructura General de la Solución

```
FleetLedger/
├── src/
│   ├── FleetLedger.Api/                  # Capa de presentación (Minimal APIs)
│   ├── FleetLedger.Domain/               # Dominio puro (sin dependencias de infraestructura)
│   ├── FleetLedger.Application/          # Casos de uso, comandos, queries e interfaces de repositorio
│   └── FleetLedger.Infrastructure/       # Implementaciones de repositorios, Marten, EF Core, PostgreSQL
├── tests/
│   ├── FleetLedger.Domain.Tests/         # Tests unitarios del agregado e invariantes
│   └── FleetLedger.Integration.Tests/    # Tests de integración con base de datos real
├── scripts/
│   └── seed/                             # Scripts de datos de demo
├── docker-compose.yml
├── docker-compose.override.yml           # Configuración de desarrollo local
└── README.md
```

## Arquitectura de Dependencias (Clean Architecture)

```
                    ┌─────────────────┐
                    │  FleetLedger.Api │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │ FleetLedger.Application │ ← Define interfaces (IDriverRepository, etc.)
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │ FleetLedger.Infrastructure │ ← Implementa las interfaces de Application
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │  FleetLedger.Domain  │
                    └─────────────────────┘
```

**Principio de Inversión de Dependencias:** Las dependencias de código apuntan hacia Application (que define puertos), pero Infrastructure implementa esos puertos. Api solo conoce Application, nunca referencia a Infrastructure directamente.

---

## Detalle por Capa

### `FleetLedger.Domain` — Dominio Puro

Contiene el núcleo del negocio. Sin referencias a infraestructura ni frameworks.

```
FleetLedger.Domain/
├── Vehicles/
│   ├── Vehicle.cs                        # Aggregate Root
│   ├── VehicleState.cs                   # Estado interno del agregado (reconstruido desde eventos)
│   ├── VehicleStatus.cs                  # Enum: Available, InTransit, InMaintenance, Decommissioned
│   ├── Severity.cs                       # Enum: Low, Medium, High, Critical
│   │
│   ├── Events/                           # Eventos del dominio (records inmutables)
│   │   ├── VehicleAcquired.cs
│   │   ├── DriverAssigned.cs
│   │   ├── DriverUnassigned.cs
│   │   ├── TripStarted.cs
│   │   ├── TripCompleted.cs
│   │   ├── TripCancelled.cs
│   │   ├── MaintenancePerformed.cs
│   │   ├── BreakdownReported.cs
│   │   ├── BreakdownResolved.cs
│   │   ├── OdometerAdjusted.cs
│   │   ├── InspectionPassed.cs
│   │   ├── InspectionFailed.cs
│   │   ├── FineIssued.cs
│   │   └── VehicleDecommissioned.cs
│   │
│   ├── Commands/                         # Intenciones del sistema antes de validar
│   │   ├── AcquireVehicleCommand.cs
│   │   └── RecordVehicleEventCommand.cs
│   │
│   └── Exceptions/                       # Excepciones de dominio (invariantes violadas)
│       ├── OdometerDecreaseException.cs
│       ├── ExpiredInspectionException.cs
│       ├── UnresolvedBreakdownException.cs
│       ├── ActiveBreakdownException.cs
│       ├── FailedInspectionBlockException.cs
│       ├── TripAlreadyInProgressException.cs
│       ├── NoTripInProgressException.cs
│       ├── DriverAlreadyAssignedException.cs
│       ├── NoDriverAssignedException.cs
│       ├── NonMonotonicTimestampException.cs
│       └── VehicleDecommissionedException.cs
│
├── Drivers/
│   ├── Driver.cs                         # Entidad de referencia (EF Core)
│   └── Exceptions/
│       └── DriverInactiveException.cs    # Lanzada en Application al validar cruce con EF Core
│
├── Depots/
│   ├── Depot.cs                          # Entidad de referencia (EF Core)
│   └── Exceptions/
│       └── DepotInactiveException.cs     # Lanzada en Application al validar cruce con EF Core
│
└── Shared/
    └── IDomainEvent.cs                   # Interfaz marker para eventos de dominio
```

**Cómo se implementa `Vehicle.cs`:**

```csharp
// El agregado solo expone métodos de comando y Apply.
// Nunca expone su estado mutable directamente.
public class Vehicle
{
    private VehicleState _state = new();

    // Reconstrucción desde eventos (llamado por Marten al cargar el stream)
    public void Apply(TripStarted @event)       => _state = _state with { Odometer = @event.OdometerKm, Status = VehicleStatus.InTransit, TripInProgress = true };
    public void Apply(TripCompleted @event)     => _state = _state with { Odometer = @event.OdometerKm, Status = VehicleStatus.Available, TripInProgress = false };
    public void Apply(TripCancelled @event)     => _state = _state with { Status = VehicleStatus.Available, TripInProgress = false };
    public void Apply(BreakdownReported @event) => _state = _state with { HasActiveBreakdown = true, Status = VehicleStatus.InMaintenance };
    public void Apply(BreakdownResolved @event) => _state = _state with { HasActiveBreakdown = false, Status = VehicleStatus.Available };
    public void Apply(DriverAssigned @event)    => _state = _state with { AssignedDriverId = @event.DriverId };
    public void Apply(DriverUnassigned @event)  => _state = _state with { AssignedDriverId = null };
    public void Apply(InspectionPassed @event)  => _state = _state with { InspectionNextDue = @event.NextDue, HasFailedInspection = false };
    public void Apply(InspectionFailed @event)  => _state = _state with { HasFailedInspection = true };
    public void Apply(OdometerAdjusted @event)  => _state = _state with { Odometer = @event.NewOdometerKm };
    public void Apply(VehicleDecommissioned @event) => _state = _state with { Status = VehicleStatus.Decommissioned };
    // ... resto de Apply

    // Validación de invariantes antes de generar el evento
    public TripStarted StartTrip(int odometerKm, string routeId, DateTime date)
    {
        if (_state.Status == VehicleStatus.Decommissioned)
            throw new VehicleDecommissionedException(Id);
        if (date < _state.LastEventTimestamp)
            throw new NonMonotonicTimestampException(_state.LastEventTimestamp, date);
        if (_state.TripInProgress)
            throw new TripAlreadyInProgressException(Id);
        if (_state.HasActiveBreakdown)
            throw new ActiveBreakdownException(Id);
        if (_state.HasFailedInspection)
            throw new FailedInspectionBlockException(Id);
        if (_state.InspectionNextDue < date)
            throw new ExpiredInspectionException(_state.InspectionNextDue);
        if (_state.AssignedDriverId is null)
            throw new NoDriverAssignedException(Id);
        if (odometerKm < _state.Odometer)
            throw new OdometerDecreaseException(_state.Odometer, odometerKm);
        return new TripStarted(Id, odometerKm, date, routeId);
    }
}
```

**Cómo se implementa `Depot.cs` (entidad EF Core):**

```csharp
// Entidad de referencia — sin eventos, sin aggregate. Solo datos relacionales.
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

    // Constructor de creación
    public static Depot Create(string name, string address, string city, ...) => ...;

    // Métodos de actualización (mutan el estado directamente — no hay eventos)
    public void Update(string name, string address, ...) { Name = name; ... UpdatedAt = DateTime.UtcNow; }
    public void Deactivate() { Active = false; UpdatedAt = DateTime.UtcNow; }
}
```

**Cómo se implementa `Driver.cs` (entidad EF Core):**

```csharp
public class Driver
{
    public string Id { get; private set; }
    public string FullName { get; private set; }
    public string LicenseNumber { get; private set; }    // único
    public string LicenseCategory { get; private set; }
    public DateOnly LicenseExpires { get; private set; }
    public string? Phone { get; private set; }
    public string? DepotId { get; private set; }         // FK opcional a Depot
    public bool Active { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Driver Create(string fullName, string licenseNumber, ...) => ...;
    public void Update(string fullName, DateOnly licenseExpires, ...) { ... UpdatedAt = DateTime.UtcNow; }
    public void Deactivate() { Active = false; UpdatedAt = DateTime.UtcNow; }
}
```

---

### `FleetLedger.Application` — Casos de Uso

Orquesta el dominio y la infraestructura. Recibe comandos, carga el agregado, y persiste eventos. Para entidades de referencia, coordina las operaciones CRUD sobre los repositorios EF Core.

```
FleetLedger.Application/
├── Ports/                                # Interfaces de repositorio (contratos con Infrastructure)
│   ├── IDriverRepository.cs
│   ├── IDepotRepository.cs
│   └── IVehicleRepository.cs
│
├── Vehicles/
│   ├── Commands/
│   │   ├── AcquireVehicleHandler.cs      # Valida DepotId activo (EF Core), emite VehicleAcquired
│   │   └── RecordEventHandler.cs         # Carga el agregado, valida referencias cruzadas, appendea evento
│   │
│   └── Queries/
│       ├── GetVehicleStatusQuery.cs       # Lee de VehicleStatusProjection
│       ├── GetVehicleTimelineQuery.cs     # Lee del event store (caso excepcional permitido)
│       ├── GetVehicleStateAtQuery.cs      # Reproduce el stream hasta un timestamp
│       └── GetVehicleCostsQuery.cs        # Lee de CostProjection
│
├── Drivers/
│   ├── Commands/
│   │   ├── CreateDriverHandler.cs        # Crea el conductor; valida licencia única y DepotId
│   │   ├── UpdateDriverHandler.cs        # Actualiza datos; re-valida DepotId si cambia
│   │   └── DeactivateDriverHandler.cs    # Soft delete — siempre permitido
│   │
│   └── Queries/
│       ├── GetDriversQuery.cs             # Lista con filtros: ?active=&depotId=&licenseCategory=
│       └── GetDriverByIdQuery.cs
│
├── Depots/
│   ├── Commands/
│   │   ├── CreateDepotHandler.cs         # Crea el depósito; valida nombre único
│   │   ├── UpdateDepotHandler.cs         # Actualiza datos; re-valida nombre único si cambia
│   │   └── DeactivateDepotHandler.cs     # Soft delete — siempre permitido
│   │
│   └── Queries/
│       ├── GetDepotsQuery.cs              # Lista con filtros: ?active=&region=
│       └── GetDepotByIdQuery.cs
│
├── Fleet/
│   └── Queries/
│       ├── GetFleetStatusQuery.cs         # Filtrable por ?depotId=
│       ├── GetFleetComplianceQuery.cs     # Filtrable por ?depotId=&status=
│       └── GetFleetCostsQuery.cs          # Filtrable por ?depotId=&from=&to=
│
└── Maintenance/
    └── Queries/
        └── GetMaintenanceAlertsQuery.cs   # Filtrable por ?depotId=&status=
```

**Patrón de un handler de comando con validación cruzada:**

```csharp
public class RecordEventHandler
{
    private readonly IVehicleRepository _vehicleRepo;   // Marten (event store)
    private readonly IDriverRepository _driverRepo;     // EF Core
    private readonly IDepotRepository _depotRepo;       // EF Core

    public async Task Handle(RecordVehicleEventCommand command, CancellationToken ct)
    {
        // 1. Validaciones cruzadas con entidades de referencia (Application layer)
        if (command is AssignDriverCommand assignCmd)
        {
            var driver = await _driverRepo.GetByIdAsync(assignCmd.DriverId, ct)
                ?? throw new DriverNotFoundException(assignCmd.DriverId);
            if (!driver.Active)
                throw new DriverInactiveException(assignCmd.DriverId);
        }

        // 2. Cargar el agregado desde el event store
        var vehicle = await _vehicleRepo.LoadAsync(command.VehicleId, ct)
            ?? throw new VehicleNotFoundException(command.VehicleId);

        // 3. Ejecutar el comando en el agregado (valida invariantes puras, genera evento)
        var @event = vehicle.Handle(command);

        // 4. Persistir el evento en el stream
        await _vehicleRepo.AppendAsync(command.VehicleId, @event, ct);
    }
}
```

---

### `FleetLedger.Infrastructure` — Persistencia y Proyecciones

Dos motores de persistencia coexistiendo en la misma PostgreSQL: Marten para el event store y EF Core para las entidades de referencia.

```
FleetLedger.Infrastructure/
# Implementa los puertos definidos en FleetLedger.Application
# No es referenciada directamente por Api - se inyecta via las interfaces de Application

├── EventStore/                           # Motor: Marten
│   ├── VehicleRepository.cs              # Carga y appendea streams via Marten
│   └── MartenConfiguration.cs            # Setup de Marten, streams, proyecciones
│
├── Projections/                          # Proyecciones async de Marten
│   ├── VehicleStatusProjection.cs        # Mantiene vehicle_status actualizado
│   ├── MaintenanceAlertsProjection.cs    # Calcula alertas por kilómetros/días
│   ├── ComplianceProjection.cs           # Rastrea vencimientos de inspecciones
│   ├── CostProjection.cs                 # Acumula costos por evento
│   └── ProjectionCheckpointProjection.cs # Registra lag y último evento por proyección
│
├── ReadModels/                           # Clases que mapean las tablas planas de lectura (Marten)
│   ├── VehicleStatusReadModel.cs
│   ├── MaintenanceAlertReadModel.cs
│   ├── ComplianceStatusReadModel.cs
│   ├── VehicleCostReadModel.cs
│   └── ProjectionHealthReadModel.cs
│
├── RelationalDb/                         # Motor: EF Core + Npgsql
│   ├── FleetLedgerDbContext.cs           # DbContext con DbSet<Driver> y DbSet<Depot>
│   ├── Configurations/
│   │   ├── DepotConfiguration.cs         # IEntityTypeConfiguration<Depot>: nombre único, índices
│   │   └── DriverConfiguration.cs        # IEntityTypeConfiguration<Driver>: licencia única, FK a Depot
│   ├── Repositories/
│   │   ├── DriverRepository.cs           # IDriverRepository — queries con filtros
│   │   └── DepotRepository.cs            # IDepotRepository — queries con filtros
│   └── Migrations/                       # Migraciones EF Core generadas con dotnet ef
│       ├── 20251001_InitialCreate.cs      # Crea tablas Depots y Drivers
│       └── FleetLedgerDbContextModelSnapshot.cs
│
└── Migrations/                           # Scripts SQL para schema de Marten (si no se usa auto-migrate)
    └── InitialSchema.sql
```

**Cómo coexisten Marten y EF Core en `Program.cs`:**

```csharp
// Ambos apuntan a la misma PostgreSQL pero son completamente independientes.
// Marten gestiona sus propias tablas (mt_events, mt_streams, etc.).
// EF Core gestiona las tablas relacionales (Depots, Drivers).

// DbContext de EF Core para entidades de referencia (Driver, Depot)
builder.Services.AddDbContext<FleetLedgerDbContext>(opts =>
    opts.UseNpgsql(connectionString));

// Marten para Event Sourcing de vehículos
builder.Services.AddMarten(opts =>
{
    opts.Connection(connectionString);
    opts.Events.StreamIdentity = StreamIdentity.AsString;
    opts.Projections.Add<VehicleStatusProjection>(ProjectionLifecycle.Async);
    opts.Projections.Add<ComplianceProjection>(ProjectionLifecycle.Async);
    opts.Projections.Add<CostProjection>(ProjectionLifecycle.Async);
    opts.Projections.Add<MaintenanceAlertsProjection>(ProjectionLifecycle.Async);
}).UseLightweightSessions();

// Registrar las implementaciones de los puertos de Application
// (Infrastructure implementa las interfaces definidas en Application)
builder.Services.AddScoped<IDriverRepository, DriverRepository>();
builder.Services.AddScoped<IDepotRepository, DepotRepository>();
builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
```

**Cómo se configura `DriverConfiguration.cs`:**

```csharp
public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.HasKey(d => d.Id);
        builder.HasIndex(d => d.LicenseNumber).IsUnique();
        builder.HasOne<Depot>()
               .WithMany()
               .HasForeignKey(d => d.DepotId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.Restrict);
        builder.Property(d => d.FullName).IsRequired().HasMaxLength(200);
        builder.Property(d => d.LicenseNumber).IsRequired().HasMaxLength(50);
        builder.Property(d => d.LicenseCategory).IsRequired().HasMaxLength(10);
    }
}
```

**Cómo se implementa una proyección en Marten:**

```csharp
// Marten llama a estos métodos cuando procesa el stream en background.
// El DepotId viene del evento VehicleAcquired y se almacena en el read model
// para habilitar el filtro GET /fleet/status?depotId=.
public class VehicleStatusProjection : MultiStreamProjection<VehicleStatusReadModel, string>
{
    public VehicleStatusProjection()
    {
        Identity<VehicleAcquired>(e => e.VehicleId);
        Identity<TripStarted>(e => e.VehicleId);
        Identity<TripCompleted>(e => e.VehicleId);
        Identity<DriverAssigned>(e => e.VehicleId);
        Identity<DriverUnassigned>(e => e.VehicleId);
        // ... resto de eventos
    }

    public void Apply(VehicleStatusReadModel model, VehicleAcquired @event)
    {
        model.Id = @event.VehicleId;
        model.DepotId = @event.DepotId;          // Almacenado para filtrar por depósito
        model.Status = "Available";
        model.Odometer = @event.InitialOdometerKm;
    }

    public void Apply(VehicleStatusReadModel model, DriverAssigned @event)
        => model.AssignedDriverId = @event.DriverId;

    public void Apply(VehicleStatusReadModel model, TripStarted @event)
    {
        model.Status = "InTransit";
        model.CurrentRoute = @event.RouteId;
        model.Odometer = @event.OdometerKm;
    }

    public void Apply(VehicleStatusReadModel model, TripCompleted @event)
    {
        model.Status = "Available";
        model.CurrentRoute = null;
        model.Odometer = @event.OdometerKm;
    }
}
```

---

### `FleetLedger.Api` — Endpoints (Minimal APIs)

```
FleetLedger.Api/
# Solo depende de FleetLedger.Application (no de Infrastructure directamente)
# Las implementaciones de repositorio se injectan via interfaces de Application

├── Program.cs                            # Composition root: DI, Marten, EF Core, Swagger, rutas
├── appsettings.json
├── appsettings.Development.json
│
├── Endpoints/
│   ├── VehicleEndpoints.cs               # Agrupa todos los endpoints de /vehicles
│   ├── FleetEndpoints.cs                 # Agrupa los endpoints de /fleet
│   ├── MaintenanceEndpoints.cs           # Agrupa los endpoints de /maintenance
│   ├── DriverEndpoints.cs                # CRUD de /drivers
│   ├── DepotEndpoints.cs                 # CRUD de /depots
│   └── ProjectionEndpoints.cs            # GET /projections/health
│
├── Contracts/                            # DTOs de request y response (no son el dominio)
│   ├── Requests/
│   │   ├── AcquireVehicleRequest.cs      # Incluye DepotId (requerido)
│   │   ├── RecordEventRequest.cs         # DTO polimórfico con discriminador "type"
│   │   ├── CreateDriverRequest.cs
│   │   ├── UpdateDriverRequest.cs
│   │   ├── CreateDepotRequest.cs
│   │   └── UpdateDepotRequest.cs
│   └── Responses/
│       ├── VehicleStatusResponse.cs
│       ├── VehicleTimelineResponse.cs
│       ├── VehicleStateAtResponse.cs
│       ├── VehicleCostsResponse.cs
│       ├── MaintenanceAlertResponse.cs
│       ├── ComplianceStatusResponse.cs
│       ├── DriverResponse.cs
│       ├── DepotResponse.cs
│       └── ProjectionHealthResponse.cs
│
└── Middleware/
    └── DomainExceptionHandler.cs         # Mapea excepciones de dominio a HTTP status codes
```

**Estructura de `DriverEndpoints.cs`:**

```csharp
public static class DriverEndpoints
{
    public static IEndpointRouteBuilder MapDriverEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/drivers").WithTags("Drivers");

        group.MapPost("/",            CreateDriver);
        group.MapGet("/",             GetDrivers);    // ?active=&depotId=&licenseCategory=
        group.MapGet("/{id}",         GetDriverById);
        group.MapPut("/{id}",         UpdateDriver);
        group.MapDelete("/{id}",      DeactivateDriver);

        return app;
    }
}
```

**Estructura de `DepotEndpoints.cs`:**

```csharp
public static class DepotEndpoints
{
    public static IEndpointRouteBuilder MapDepotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/depots").WithTags("Depots");

        group.MapPost("/",            CreateDepot);
        group.MapGet("/",             GetDepots);     // ?active=&region=
        group.MapGet("/{id}",         GetDepotById);
        group.MapPut("/{id}",         UpdateDepot);
        group.MapDelete("/{id}",      DeactivateDepot);

        return app;
    }
}
```

**Mapeo de excepciones de dominio a HTTP en `DomainExceptionHandler.cs`:**

```csharp
// Vehículos (Event Sourcing)
// OdometerDecreaseException           → 422 Unprocessable Entity
// ExpiredInspectionException           → 422 Unprocessable Entity
// UnresolvedBreakdownException         → 422 Unprocessable Entity
// ActiveBreakdownException             → 422 Unprocessable Entity
// FailedInspectionBlockException       → 422 Unprocessable Entity
// TripAlreadyInProgressException       → 422 Unprocessable Entity
// NoTripInProgressException            → 422 Unprocessable Entity
// DriverAlreadyAssignedException       → 422 Unprocessable Entity
// NoDriverAssignedException            → 422 Unprocessable Entity
// NonMonotonicTimestampException       → 422 Unprocessable Entity
// VehicleDecommissionedException       → 409 Conflict
// VehicleNotFoundException             → 404 Not Found
// VinAlreadyExistsException            → 409 Conflict

// Referencias cruzadas (Application layer)
// DriverNotFoundException              → 404 Not Found
// DriverInactiveException              → 422 Unprocessable Entity
// DepotNotFoundException               → 404 Not Found
// DepotInactiveException               → 422 Unprocessable Entity

// CRUD de entidades de referencia (EF Core)
// LicenseNumberAlreadyExistsException  → 409 Conflict
// DepotNameAlreadyExistsException      → 409 Conflict
```

---

### `tests/FleetLedger.Domain.Tests`

```
FleetLedger.Domain.Tests/
├── Vehicles/
│   ├── VehicleInvariantsTests.cs         # Tests de todas las invariantes del agregado puro
│   │   ├── OdometerMustBeIncreasing
│   │   ├── CannotStartTripWithExpiredInspection
│   │   ├── CannotStartTripWithActiveBreakdown
│   │   ├── CannotStartTripWithFailedInspection
│   │   ├── CannotStartTripWithoutDriver
│   │   ├── CannotStartTripWhenTripInProgress
│   │   ├── CannotAssignDriverWhenAlreadyAssigned
│   │   ├── CannotUnassignDriverWhenNoneAssigned
│   │   ├── CannotOperateDecommissionedVehicle
│   │   ├── TimestampMustBeMonotonic
│   │   ├── BreakdownResolved_DoesNotRequireMaintenancePerformed
│   │   └── OdometerAdjusted_BackwardRequiresApprovedBy
│   │
│   └── VehicleStateReconstructionTests.cs # Tests de reconstrucción desde eventos
│       ├── StateAfterTripCompleted
│       ├── StateAfterBreakdownAndResolution
│       ├── StateAfterDriverAssignedAndUnassigned
│       └── StateAfterDecommission
│
└── Drivers/
    └── DriverEntityTests.cs              # Tests de las reglas de Depot/Driver como entidades simples
        ├── Deactivate_SetsActiveFalse
        └── Update_SetsUpdatedAt
```

---

### `tests/FleetLedger.Integration.Tests`

```
FleetLedger.Integration.Tests/
├── Fixtures/
│   └── DatabaseFixture.cs                # Levanta PostgreSQL con Testcontainers
│                                         # Aplica migraciones EF Core + schema de Marten
│
├── Scenarios/
│   ├── VehicleLifecycleScenarioTests.cs  # Escenario completo: depot → driver → vehicle → eventos
│   └── AuditScenarioTests.cs             # Verifica que point-in-time responde correctamente
│
└── Endpoints/
    ├── VehicleEndpointsTests.cs
    ├── FleetEndpointsTests.cs
    ├── MaintenanceEndpointsTests.cs
    ├── DriverEndpointsTests.cs           # CRUD de conductores + validaciones
    └── DepotEndpointsTests.cs            # CRUD de depósitos + validaciones
```

**Nombres de test de integración relevantes (cruce de contextos):**

```csharp
// DriverEndpointsTests.cs
DriverAssigned_WithInactiveDriver_Returns422
CreateDriver_WithDuplicateLicense_Returns409
DeleteDriver_WithHistoricalAssignments_Returns200   // soft delete siempre permitido
GetDrivers_FilteredByDepot_ReturnsOnlyDepotDrivers

// DepotEndpointsTests.cs
VehicleAcquired_WithInactiveDepot_Returns422
CreateDepot_WithDuplicateName_Returns409
DeleteDepot_WithAssociatedVehicles_Returns200       // soft delete siempre permitido

// VehicleLifecycleScenarioTests.cs
FullScenario_CreateDepotDriverVehicle_ThenRecordEvents
StateAt_ReturnsCorrectDriverBeforeUnassignment
FleetStatus_FilteredByDepot_ReturnsOnlyDepotVehicles
```

---

### `scripts/seed`

```
scripts/seed/
├── demo_scenario.http                    # Archivo .http con los requests del escenario de demo
└── SeedData.cs                           # Clase C# que ejecuta el seed en startup de desarrollo
```

El escenario de demo genera la siguiente historia para el sistema completo:

**Entidades de referencia (EF Core):**
```
POST /depots    → DEP-001 "Buenos Aires Norte", Ciudad Autónoma de Buenos Aires
POST /drivers   → DRV-009 "Carlos Méndez", licencia C, depotId: DEP-001
POST /drivers   → DRV-014 "Luis Torres",   licencia D, depotId: DEP-001
```

**Stream de eventos (Marten) para `demo-truck-01`:**
```
VehicleAcquired         (2025-10-01, depotId: DEP-001)
DriverAssigned          (2025-10-02, driverId: DRV-009)
TripStarted             (2025-10-03, km: 0)
TripCompleted           (2025-10-03, km: 520)
InspectionPassed        (2025-10-15, next: 2026-04-15)
TripStarted             (2025-11-01, km: 520)
TripCompleted           (2025-11-01, km: 1240)
MaintenancePerformed    (2025-12-01, type: OilChange, km: 5000)
TripStarted             (2026-01-10, km: 5000)
BreakdownReported       (2026-01-10, brakes overheating, km: 5200)
  → intentar TripStarted aquí → 422 ActiveBreakdownException ✓
BreakdownResolved       (2026-01-12)
  → sin MaintenancePerformed previo — igual funciona ✓
TripStarted             (2026-02-01, km: 5200)
TripCompleted           (2026-02-01, km: 6800)
FineIssued              (2026-02-15, driverId: DRV-009, speeding, $150)
DriverUnassigned        (2026-03-01)
DriverAssigned          (2026-03-01, driverId: DRV-014)
TripStarted             (2026-03-15, km: 6800)
TripCompleted           (2026-03-20, km: 9500)
```

**Preguntas que el sistema responde en el demo:**

```
GET /vehicles/demo-truck-01/state-at?date=2026-01-11T10:00:00Z
→ HasActiveBreakdown: true, AssignedDriverId: DRV-009

GET /drivers/DRV-009
→ FullName: "Carlos Méndez"   (el cliente resuelve el nombre a partir del ID del evento)

GET /fleet/status?depotId=DEP-001
→ demo-truck-01 aparece en el resultado

GET /fleet/costs?depotId=DEP-001&from=2026-01-01&to=2026-12-31
→ Costos de multas y mantenimiento del depósito

GET /maintenance/alerts?depotId=DEP-001&status=upcoming
→ BrakeService próximo (nunca se realizó un reemplazo completo)

GET /projections/health
→ lag en segundos y estado healthy/degraded por proyección
```

Con este stream, el endpoint `GET /vehicles/demo-truck-01/state-at?date=2026-01-11T10:00:00Z` debe devolver que había una avería activa sin resolver — exactamente el tipo de pregunta que se hace en auditorías legales.

---

## Convenciones del Proyecto

### Naming de Streams en Marten
Los streams de vehículos usan el ID del vehículo directamente como stream key. Ejemplo: `vehicle-abc123`. Las entidades de referencia (`Driver`, `Depot`) no generan streams; son filas en tablas relacionales gestionadas por EF Core.

### Dos contextos de persistencia, una sola PostgreSQL
- **Marten** gestiona sus propias tablas (`mt_events`, `mt_streams`, tablas de proyecciones). No interfiere con las tablas de EF Core.
- **EF Core** gestiona las tablas `Depots` y `Drivers` con migraciones convencionales.
- La integridad referencial entre ambos motores es **suave**: el Application layer valida que los IDs existan antes de persistir eventos, pero no hay foreign keys cruzadas entre el event store y las tablas relacionales.

### Separación comando / query en endpoints
- Verbos `POST/PUT/DELETE` sobre `/vehicles` → operaciones de escritura → cargan el agregado → validan invariantes y referencias cruzadas → appendean al stream.
- Verbos `POST/PUT/DELETE` sobre `/drivers` y `/depots` → operaciones CRUD sobre EF Core directamente.
- Verbos `GET` sobre `/vehicles`, `/fleet`, `/maintenance` → consultan proyecciones (excepto `/timeline` y `/state-at`).
- Verbos `GET` sobre `/drivers` y `/depots` → consultan tablas relacionales vía EF Core.

### DTOs vs Dominio
Los contratos de la API (`Requests/`, `Responses/`) son DTOs planos que no exponen los tipos del dominio. El mapeo ocurre en los handlers o en los endpoints.

### Manejo de errores
Todas las excepciones de dominio y de referencia cruzada se propagan hasta el middleware y se mapean a códigos HTTP descriptivos. Los mensajes de error en `application/problem+json` incluyen el motivo específico de la violación (ej: `"Driver DRV-009 is inactive and cannot be assigned"`).
