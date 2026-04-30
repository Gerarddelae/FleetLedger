# Resumen de Implementación — Día 4

## Fecha: 28 de Abril de 2026

## Objetivo
Validación cruzada y refinamiento del CRUD de Driver y Depot. Implementar FluentValidation, middleware de excepciones centralizado, y tests de integración.

---

## Estructura de Proyectos Modificados

```
FleetLedger/
├── src/
│   ├── FleetLedger.Domain/
│   │   └── Exceptions/
│   │       ├── LicenseNumberAlreadyExistsException.cs  ← Nueva
│   │       ├── DepotNameAlreadyExistsException.cs      ← Nueva
│   │       ├── DriverNotFoundException.cs             ← Nueva
│   │       ├── DepotNotFoundException.cs              ← Nueva
│   │       ├── DriverInactiveException.cs             ← Nueva
│   │       └── DepotInactiveException.cs               ← Nueva
│   │
│   ├── FleetLedger.Api/
│   │   ├── Contracts/Requests/
│   │   │   ├── DriverRequests.cs                      ← Nuevo DTO
│   │   │   └── DepotRequests.cs                      ← Nuevo DTO
│   │   ├── Validators/
│   │   │   ├── CreateDriverRequestValidator.cs         ← Nuevo FluentValidation
│   │   │   └── CreateDepotRequestValidator.cs       ← Nuevo FluentValidation
│   │   ├── Middleware/
│   │   │   └── DomainExceptionMiddleware.cs         ← Nuevo manejo centralizado
│   │   └── Program.cs                              ← Actualizado
│   │
│   └── FleetLedger.Application/
│   │   └── Handlers/
│   │       ├── DriverHandler.cs    ← Actualizado con nuevas excepciones
│   │       └── DepotHandler.cs   ← Actualizado con nuevas excepciones
│
└── tests/
    └── FleetLedger.Integration.Tests/
        ├── Fixtures/
        │   └── DatabaseFixture.cs    ←Fixture con Testcontainers
        └── Endpoints/
            ├── DepotEndpointsTests.cs   ← 8 tests
            └── DriverEndpointsTests.cs  ← 10 tests
```

---

## Excepciones de Dominio ( Nuevas )

### 409 Conflict
```csharp
public class LicenseNumberAlreadyExistsException : Exception
{
    public string LicenseNumber { get; }
    public LicenseNumberAlreadyExistsException(string licenseNumber)
        : base($"A driver with license number '{licenseNumber}' already exists.") { }
}

public class DepotNameAlreadyExistsException : Exception
{
    public string Name { get; }
    public DepotNameAlreadyExistsException(string name)
        : base($"A depot with name '{name}' already exists.") { }
}
```

### 404 Not Found
```csharp
public class DriverNotFoundException : Exception
{
    public string DriverId { get; }
    public DriverNotFoundException(string driverId)
        : base($"Driver '{driverId}' not found.") { }
}

public class DepotNotFoundException : Exception
{
    public string DepotId { get; }
    public DepotNotFoundException(string depotId)
        : base($"Depot '{depotId}' not found.") { }
}
```

### 422 Unprocessable Entity
```csharp
public class DriverInactiveException : Exception
{
    public string DriverId { get; }
    public DriverInactiveException(string driverId)
        : base($"Driver '{driverId}' is inactive and cannot be assigned.") { }
}

public class DepotInactiveException : Exception
{
    public string DepotId { get; }
    public DepotInactiveException(string depotId)
        : base($"Depot '{depotId}' is inactive and cannot be assigned to a new driver.") { }
}
```

---

## Middleware de Excepciones

El `DomainExceptionMiddleware` intercepta todas las excepciones no manejadas y las convierte a `ProblemDetails` con códigos HTTP apropiados:

| Excepción | HTTP Status | errorCode |
|---|---|---|
| `LicenseNumberAlreadyExistsException` | 409 | `LICENSE_NUMBER_ALREADY_EXISTS` |
| `DepotNameAlreadyExistsException` | 409 | `DEPOT_NAME_ALREADY_EXISTS` |
| `DriverNotFoundException` | 404 | `DRIVER_NOT_FOUND` |
| `DepotNotFoundException` | 404 | `DEPOT_NOT_FOUND` |
| `DriverInactiveException` | 422 | `DRIVER_INACTIVE` |
| `DepotInactiveException` | 422 | `DEPOT_INACTIVE` |

Ejemplo de respuesta:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.3.5",
  "title": "Conflict",
  "status": 409,
  "detail": "A driver with license number 'LICENSE123' already exists.",
  "extensions": {
    "licenseNumber": "LICENSE123",
    "errorCode": "LICENSE_NUMBER_ALREADY_EXISTS"
  }
}
```

---

## Validadores (FluentValidation)

### CreateDriverRequestValidator
```csharp
RuleFor(x => x.FullName)
    .NotEmpty().WithMessage("FullName is required.")
    .MaximumLength(200).WithMessage("FullName must not exceed 200 characters.");

RuleFor(x => x.LicenseNumber)
    .NotEmpty().WithMessage("LicenseNumber is required.")
    .MaximumLength(50).WithMessage("LicenseNumber must not exceed 50 characters.");

RuleFor(x => x.LicenseCategory)
    .NotEmpty().WithMessage("LicenseCategory is required.")
    .MaximumLength(10).WithMessage("LicenseCategory must not exceed 10 characters.");

RuleFor(x => x.LicenseExpires)
    .NotEmpty().WithMessage("LicenseExpires is required.")
    .GreaterThan(DateOnly.FromDateTime(DateTime.Today))
    .WithMessage("License must not be expired.");

RuleFor(x => x.Phone)
    .MaximumLength(50).WithMessage("Phone must not exceed 50 characters.")
    .Matches(@"^\+?[\d\s\-()*$").When(x => !string.IsNullOrEmpty(x.Phone))
    .WithMessage("Phone must be a valid phone number.");
```

### CreateDepotRequestValidator
```csharp
RuleFor(x => x.Name)
    .NotEmpty().WithMessage("Name is required.")
    .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

RuleFor(x => x.Address)
    .NotEmpty().WithMessage("Address is required.")
    .MaximumLength(500).WithMessage("Address must not exceed 500 characters.");

RuleFor(x => x.City)
    .NotEmpty().WithMessage("City is required.")
    .MaximumLength(200).WithMessage("City must not exceed 200 characters.");
```

---

## DTOs de Request

```csharp
// DriverRequests.cs
public record CreateDriverRequest(
    string FullName,
    string LicenseNumber,
    string LicenseCategory,
    DateOnly LicenseExpires,
    string? Phone = null,
    string? DepotId = null
);

public record UpdateDriverRequest(
    string FullName,
    string LicenseNumber,
    string LicenseCategory,
    DateOnly LicenseExpires,
    string? Phone = null,
    string? DepotId = null
);

// DepotRequests.cs
public record CreateDepotRequest(
    string Name,
    string Address,
    string City,
    string? Region = null,
    string? ManagerName = null,
    string? Phone = null
);

public record UpdateDepotRequest(
    string Name,
    string Address,
    string City,
    string? Region = null,
    string? ManagerName = null,
    string? Phone = null
);
```

---

## Configuración en Program.cs

```csharp
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateDriverRequestValidator>();
// ...
app.UseDomainExceptionHandler();
```

---

## Tests de Integración

### Paquetes Instalados
| Paquete | Versión |
|---|---|
| Testcontainers.PostgreSql | 4.1.0 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.7 |

### DepotEndpointsTests (~8 tests)
- `CreateDepot_Returns201_AndValidId`
- `CreateDepot_DuplicateName_Returns409`
- `GetDepot_Existing_Returns200`
- `GetDepot_NonExisting_Returns404`
- `UpdateDepot_Existing_Returns200`
- `UpdateDepot_DuplicateName_Returns409`
- `DeleteDepot_Existing_Returns204_AndSetsInactive`
- `GetDepots_FilterByActive_ReturnsFiltered`

### DriverEndpointsTests (~10 tests)
- `CreateDriver_Returns201_AndValidId`
- `CreateDriver_DuplicateLicense_Returns409`
- `CreateDriver_WithInactiveDepot_Returns422`
- `GetDriver_Existing_Returns200`
- `GetDriver_NonExisting_Returns404`
- `UpdateDriver_Existing_Returns200`
- `UpdateDriver_DuplicateLicense_Returns409`
- `DeleteDriver_Existing_Returns204_AndSetsInactive`
- `GetDrivers_FilterByDepot_ReturnsOnlyDriversOfThatDepot`
- `GetDrivers_FilterByLicenseCategory_ReturnsFiltered`

### Fixture de Base de Datos
```csharp
public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("fleetledger")
        .WithUsername("fleet")
        .WithPassword("fleet")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();
    public async Task InitializeAsync() => await _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();
}
```

---

## Estado del Build

```
Compilación correcta.
    0 Errores
    1 Advertencia (no crítica - versión de EF Core en tests)
```

---

## Cambios en Handlers

Los handlers ahora usan las nuevas excepciones específicas:

```csharp
// DriverHandler.cs
public async Task<Driver> Handle(CreateDriverCommand cmd, CancellationToken ct)
{
    var exists = await _repository.ExistsWithLicenseNumberAsync(cmd.LicenseNumber, ct);
    if (exists)
        throw new LicenseNumberAlreadyExistsException(cmd.LicenseNumber);

    if (!string.IsNullOrEmpty(cmd.DepotId))
    {
        var depot = await _depotRepository.GetByIdAsync(cmd.DepotId, ct)
            ?? throw new DepotNotFoundException(cmd.DepotId);
        if (!depot.Active)
            throw new DepotInactiveException(cmd.DepotId);
    }
    // ...
}
```

---

## Entragables del Día 4

- ✅ Validación de input con FluentValidation produce `400` con errores descriptivos
- ✅ Duplicados de nombre/licencia → `409` con mensaje específico
- ✅ Referencias inválidas (depot inactivo) → `422` con mensaje descriptivo
- ✅ ~18 tests de integración configurados cubriendo CRUD completo
- ✅ Swagger documenta todos los códigos de error por endpoint
- ✅ Controladores delegan manejo de errores al middleware
- ✅ Base sólida para entrar a Fase 2 (Event Sourcing con Marten) en el Día 5

---

## Siguiente Paso

**Día 5**: Fase 2 - Core del Event Sourcing
- Instalar Marten para Event Sourcing
- Definir los 15 eventos del dominio de vehículos
- Implementar el aggregate `Vehicle` y `VehicleState`
- Configurar el event store