# Guía de Pruebas - FleetLedger API

## Requisitos Previos

```bash
# Levantar PostgreSQL
docker compose up -d

# Levantar la API
dotnet run --project src/FleetLedger.Api
```

La API estará disponible en: **http://localhost:5000**

Swagger UI: **http://localhost:5000/**

---

## Pruebas con cURL

### 1. Depots (Depósitos)

#### Crear Depot - 201 Created
```bash
curl -X POST http://localhost:5000/depots \
  -H "Content-Type: application/json" \
  -d '{"name":"Buenos Aires Norte","address":"Av. Principal 123","city":"Buenos Aires","region":"CABA"}'
```

**Respuesta esperada:**
```json
{
  "id": "DEP-20260428-0001",
  "name": "Buenos Aires Norte",
  "address": "Av. Principal 123",
  "city": "Buenos Aires",
  "region": "CABA",
  "managerName": null,
  "phone": null,
  "active": true,
  "createdAt": "2026-04-28T...",
  "updatedAt": "2026-04-28T..."
}
```

---

#### Listar todos los Depots - 200 OK
```bash
curl http://localhost:5000/depots
```

---

#### Buscar Depot por ID - 200 OK
```bash
curl http://localhost:5000/depots/DEP-20260428-0001
```

---

#### Buscar Depot inexistente - 404 Not Found
```bash
curl http://localhost:5000/depots/DEP-INEXISTENTE
```

**Respuesta:**
```json
{
  "error": "Depot 'DEP-INEXISTENTE' not found."
}
```

---

#### Crear Depot con nombre duplicado - 409 Conflict
```bash
# Primero crear uno
curl -X POST http://localhost:5000/depots \
  -H "Content-Type: application/json" \
  -d '{"name":"Depot Duplicado","address":"Calle 1","city":"Ciudad"}'

# Intentar crear otro con el mismo nombre
curl -X POST http://localhost:5000/depots \
  -H "Content-Type: application/json" \
  -d '{"name":"Depot Duplicado","address":"Calle 2","city":"Ciudad"}'
```

**Respuesta:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.3.5",
  "title": "Conflict",
  "status": 409,
  "detail": "A depot with name 'Depot Duplicado' already exists.",
  "extensions": {
    "name": "Depot Duplicado",
    "errorCode": "DEPOT_NAME_ALREADY_EXISTS"
  }
}
```

---

#### Actualizar Depot - 200 OK
```bash
curl -X PUT http://localhost:5000/depots/DEP-20260428-0001 \
  -H "Content-Type: application/json" \
  -d '{"name":"Buenos Aires Actualizado","address":"Nueva Direccion","city":"Buenos Aires","region":"GBA"}'
```

---

#### Soft Delete Depot - 204 No Content
```bash
curl -X DELETE http://localhost:5000/depots/DEP-20260428-0001
```

---

#### Filtrar Depots activos/inactivos - 200 OK
```bash
# Solo activos
curl "http://localhost:5000/depots?active=true"

# Solo inactivos
curl "http://localhost:5000/depots?active=false"

# Por region
curl "http://localhost:5000/depots?region=CABA"
```

---

### 2. Drivers (Conductores)

#### Crear Driver - 201 Created
```bash
curl -X POST http://localhost:5000/drivers \
  -H "Content-Type: application/json" \
  -d '{"fullName":"Carlos Mendez","licenseNumber":"LIC123456","licenseCategory":"C","licenseExpires":"2027-12-31"}'
```

**Respuesta esperada:**
```json
{
  "id": "DRV-20260428-0001",
  "fullName": "Carlos Mendez",
  "licenseNumber": "LIC123456",
  "licenseCategory": "C",
  "licenseExpires": "2027-12-31",
  "phone": null,
  "depotId": null,
  "active": true,
  "createdAt": "2026-04-28T...",
  "updatedAt": "2026-04-28T..."
}
```

---

#### Crear Driver con Depot - 201 Created
```bash
# Primero crear un Depot
DEP_ID=$(curl -s -X POST http://localhost:5000/depots \
  -H "Content-Type: application/json" \
  -d '{"name":"Depot Driver","address":"Calle","city":"Ciudad"}' | jq -r '.id')

# Crear Driver asignando al Depot
curl -X POST http://localhost:5000/drivers \
  -H "Content-Type: application/json" \
  -d "{\"fullName\":\"Juan Perez\",\"licenseNumber\":\"LIC789\",\"licenseCategory\":\"C\",\"licenseExpires\":\"2027-12-31\",\"depotId\":\"$DEP_ID\"}"
```

---

#### Listar Drivers - 200 OK
```bash
curl http://localhost:5000/drivers
```

---

#### Buscar Driver por ID - 200 OK
```bash
curl http://localhost:5000/drivers/DRV-20260428-0001
```

---

#### Buscar Driver inexistente - 404 Not Found
```bash
curl http://localhost:5000/drivers/DRV-INEXISTENTE
```

**Respuesta:**
```json
{
  "error": "Driver 'DRV-INEXISTENTE' not found."
}
```

---

#### Crear Driver con licencia duplicada - 409 Conflict
```bash
# Crear primer driver
curl -X POST http://localhost:5000/drivers \
  -H "Content-Type: application/json" \
  -d '{"fullName":"Driver Uno","licenseNumber":"DUPLICADO1","licenseCategory":"C","licenseExpires":"2027-12-31"}'

# Intentar crear otro con la misma licencia
curl -X POST http://localhost:5000/drivers \
  -H "Content-Type: application/json" \
  -d '{"fullName":"Driver Dos","licenseNumber":"DUPLICADO1","licenseCategory":"C","licenseExpires":"2027-12-31"}'
```

**Respuesta:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.3.5",
  "title": "Conflict",
  "status": 409,
  "detail": "A driver with license number 'DUPLICADO1' already exists.",
  "extensions": {
    "licenseNumber": "DUPLICADO1",
    "errorCode": "LICENSE_NUMBER_ALREADY_EXISTS"
  }
}
```

---

#### Crear Driver con Depot inactivo - 422 Unprocessable Entity
```bash
# Crear Depot
DEP_ID=$(curl -s -X POST http://localhost:5000/depots \
  -H "Content-Type: application/json" \
  -d '{"name":"Depot Inactivo","address":"Calle","city":"Ciudad"}' | jq -r '.id')

# Inactivar el Depot
curl -X DELETE http://localhost:5000/depots/$DEP_ID

# Intentar crear Driver con Depot inactivo
curl -X POST http://localhost:5000/drivers \
  -H "Content-Type: application/json" \
  -d "{\"fullName\":\"Driver Error\",\"licenseNumber\":\"ERROR1\",\"licenseCategory\":\"C\",\"licenseExpires\":\"2027-12-31\",\"depotId\":\"$DEP_ID\"}"
```

**Respuesta:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.24",
  "title": "Unprocessable Entity",
  "status": 422,
  "detail": "Depot '$DEP_ID' is inactive and cannot be assigned to a new driver.",
  "extensions": {
    "depotId": "$DEP_ID",
    "errorCode": "DEPOT_INACTIVE"
  }
}
```

---

#### Validation Error (campos vacíos) - 400 Bad Request
```bash
curl -X POST http://localhost:5000/drivers \
  -H "Content-Type: application/json" \
  -d '{"fullName":"","licenseNumber":"","licenseCategory":"","licenseExpires":"2025-01-01"}'
```

**Respuesta:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "FullName": [ "FullName is required." ],
    "LicenseNumber": [ "LicenseNumber is required." ],
    "LicenseCategory": [ "LicenseCategory is required." ],
    "LicenseExpires": [ "License must not be expired." ]
  }
}
```

---

#### Filtrar Drivers por Depot - 200 OK
```bash
curl "http://localhost:5000/drivers?depotId=DEP-20260428-0001"
```

---

#### Filtrar Drivers por licencia - 200 OK
```bash
curl "http://localhost:5000/drivers?licenseCategory=C"
```

---

#### Filtrar Drivers activos - 200 OK
```bash
curl "http://localhost:5000/drivers?active=true"
```

---

#### Actualizar Driver - 200 OK
```bash
curl -X PUT http://localhost:5000/drivers/DRV-20260428-0001 \
  -H "Content-Type: application/json" \
  -d '{"fullName":"Carlos Actualizado","licenseNumber":"LIC NUEVO","licenseCategory":"D","licenseExpires":"2028-01-01"}'
```

---

#### Soft Delete Driver - 204 No Content
```bash
curl -X DELETE http://localhost:5000/drivers/DRV-20260428-0001
```

---

### 3. Health Check

```bash
curl http://localhost:5000/health
```

**Respuesta:**
```json
{
  "status": "healthy",
  "timestamp": "2026-04-28T12:34:56.789Z"
}
```

---

## Resumen de Códigos de Respuesta

| Código | Significado | Cuándo ocurre |
|--------|------------|--------------|
| 200 | OK | GET, PUT exitoso |
| 201 | Created | POST exitoso |
| 204 | No Content | DELETE exitoso |
| 400 | Bad Request | Validation error (FluentValidation) |
| 404 | Not Found | Driver/Depot no encontrado |
| 409 | Conflict | Nombre o licencia duplicada |
| 422 | Unprocessable Entity | Depot inactivo |

---

## Cómo Correr Tests de Integración

### Requisitos
- Docker Desktop ejecutándose
- PostgreSQL disponible (puede ser el mismo de docker-compose o el de testcontainers)

### Comandos

#### Correr todos los tests
```bash
dotnet test
```

#### Correr solo tests de integración
```bash
dotnet test tests/FleetLedger.Integration.Tests
```

#### Correr con verbose
```bash
dotnet test tests/FleetLedger.Integration.Tests -v n
```

#### Correr test específico por nombre
```bash
dotnet test tests/FleetLedger.Integration.Tests --filter "FullyQualifiedName~CreateDepot_Returns201"
```

#### Correr solo tests de Depot
```bash
dotnet test tests/FleetLedger.Integration.Tests --filter "FullyQualifiedName~DepotEndpointsTests"
```

#### Correr solo tests de Driver
```bash
dotnet test tests/FleetLedger.Integration.Tests --filter "FullyQualifiedName~DriverEndpointsTests"
```

#### Ver cobertura
```bash
dotnet test tests/FleetLedger.Integration.Tests --collect:"XPlat Code Coverage"
```

### Estructura de Tests

```csharp
// Fixture compartida (se ejecuta una vez por colección)
[Collection("Integration Tests")]
public class DepotEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    // La fixture provee:
    // - HttpClient factory.CreateClient()
    // - Base URL configurada automáticamente
}

// Tests usan assertions de xUnit
Assert.Equal(HttpStatusCode.Created, response.StatusCode);
Assert.NotNull(depot);
```

---

## Explicación de Cómo Funcionan los Tests en .NET

### Framework: xUnit

xUnit es el framework de testing más usado en .NET. Funciona así:

#### 1. Fixture de tests

Una **fixture** es código que se ejecuta antes/después de los tests:

```csharp
// IClassFixture<T> - una instancia por clase de test
public class DatabaseFixture : IAsyncLifetime
{
    public async Task InitializeAsync() { }  // Se ejecuta antes de los tests
    public async Task DisposeAsync() { }     // Se ejecuta después de los tests
}

// ICollectionFixture<T> - una instancia por colección de tests
[CollectionDefinition("Integration Tests")]
public class IntegrationTestsCollection : ICollectionFixture<DatabaseFixture>
{
}
```

#### 2. Colecciones

Los tests se agrupan en **colecciones** para compartir fixtures:

```csharp
[Collection("Integration Tests")]  // Comparte DatabaseFixture
public class DepotEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    public DepotEndpointsTests(TestWebApplicationFactory factory) { }
}
```

#### 3. Assertions

Las **assertions** verificar el comportamiento esperado:

```csharp
// Equality
Assert.Equal(expected, actual);

// Null
Assert.NotNull(value);
Assert.Null(value);

// Contains
Assert.Contains(items, item => item.Id == id);

// Status codes
Assert.Equal(HttpStatusCode.OK, response.StatusCode);

// Type
Assert.IsType<Driver>(result);
```

#### 4. Arrange-Act-Assert

Estructura estándar de un test:

```csharp
[Fact]  // Hecho (test individual)
public void CreateDepot_Returns201_AndValidId()
{
    // Arrange - preparar datos
    var request = new CreateDepotRequest("Test", "Address", "City");

    // Act - ejecutar la acción
    var response = await _client.PostAsJsonAsync("/depots", request);

    // Assert - verificar resultado
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
}
```

#### 5. Test Containers

Los tests de integración usan **Testcontainers** para:
- Levantar PostgreSQL en un container Docker
- Ejecutar migraciones automáticamente
- Limpiar después de cada test

```csharp
private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
    .WithImage("postgres:16")
    .WithDatabase("fleetledger")
    .Build();
```

#### 6. WebApplicationFactory

Para tests de API web:

```csharp
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", connectionString);
    }
}
```

Esto configura la aplicación para usar PostgreSQL de test en lugar de la real.

---

## Referencias

- **xUnit**: https://xunit.net/
- **Testcontainers**: https://testcontainers.com/
- **FluentValidation**: https://docs.fluentvalidation.net/