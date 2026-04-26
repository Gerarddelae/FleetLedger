# Resumen de Implementación — Día 2

## Fecha: 25 de Abril de 2026

## Objetivo
Implementar entidad `Depot` con EF Core (CRUD completo)

---

## Estructura de Proyectos Modificados

```
FleetLedger/
├── src/
│   ├── FleetLedger.Domain/
│   │   └── Depot.cs                    ← Nueva entidad
│   ├── FleetLedger.Infrastructure/
│   │   ├── FleetLedgerDbContext.cs     ← DbContext + Configuration
│   │   ├── DepotRepository.cs          ← Implementación de repositorio
│   │   ├── FleetLedgerDbContextFactory.cs  ← Factory para design-time
│   │   └── Data/Migrations/          ← Migración aplicada
│   ├── FleetLedger.Application/
│   │   ├── IDepotRepository.cs     ← Interfaz de repositorio
│   │   ├── DepotCommands.cs        ← Commands/Queries
│   │   └── Handlers/
│   │       └── DepotHandler.cs      ← Handler
│   └── FleetLedger.Api/
│       ├── Controllers/
│       │   └── DepotsController.cs  ← 5 endpoints REST
│       └── Program.cs                ← Configuración DI + EF Core
```

---

## Entidad Depot (Domain)

```csharp
public class Depot
{
    public string Id { get; private set; }      // formato: DEP-20260425-XXXX
    public string Name { get; private set; }     // único
    public string Address { get; private set; }
    public string City { get; private set; }
    public string? Region { get; private set; }
    public string? ManagerName { get; private set; }
    public string? Phone { get; private set; }
    public bool Active { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Depot Create(...)   // Factory
    public void Update(...)       // Actualizar datos
    public void Deactivate()    // Soft delete
    public void Activate()     // Reactivar
}
```

---

## DbContext y Configuration (Infrastructure)

```csharp
public class FleetLedgerDbContext : DbContext
{
    public DbSet<Depot> Depots { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.ApplyConfiguration(new DepotConfiguration());
    }
}

public class DepotConfiguration : IEntityTypeConfiguration<Depot>
{
    public void Configure(EntityTypeBuilder<Depot> builder)
    {
        builder.ToTable("depots");
        builder.HasKey(d => d.Id);
        builder.HasIndex(d => d.Name).IsUnique();  // Nombre único
        builder.Property(d => d.Active).HasDefaultValue(true);
    }
}
```

---

## Interfaz y Repositorio (Application)

```csharp
public interface IDepotRepository
{
    Task<Depot?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<List<Depot>> GetAllAsync(bool? active = null, string? region = null, CancellationToken ct = default);
    Task<Depot> AddAsync(Depot depot, CancellationToken ct = default);
    Task UpdateAsync(Depot depot, CancellationToken ct = default);
    Task<bool> ExistsWithNameAsync(string name, CancellationToken ct = default);
    Task<Depot?> FindByNameAsync(string name, CancellationToken ct = default);
}
```

---

## Commands y Queries (Application)

```csharp
public record CreateDepotCommand(
    string Name,
    string Address,
    string City,
    string? Region = null,
    string? ManagerName = null,
    string? Phone = null
);

public record UpdateDepotCommand(
    string Id,
    string Name,
    string Address,
    string City,
    string? Region,
    string? ManagerName,
    string? Phone
);

public record DeactivateDepotCommand(string Id);
public record GetDepotsQuery(bool? Active = null, string? Region = null);
public record GetDepotByIdQuery(string Id);
```

---

## Endpoints API (FleetLedger.Api)

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| POST | `/depots` | Crear depósito |
| GET | `/depots` | Listar depósitos (filtros: `?active=&region=`) |
| GET | `/depots/{id}` | Obtener depósito por ID |
| PUT | `/depots/{id}` | Actualizar datos del depósito |
| DELETE | `/depots/{id}` | Soft delete (Active = false) |

---

## Reglas de Negocio Implementadas

- **Nombre único:** `409 Conflict` si se intenta crear/actualizar con nombre duplicado
- **Soft delete:** `DELETE` no elimina físicamente, pone `Active = false`
- **Filtros en GET:** `?active=true` / `?active=false` / `?region=CABA`
- La validación de nombre duplicado se hace en el handler antes de persistir

---

## Paquetes NuGet Instalados

| Proyecto | Paquete | Versión |
|----------|---------|---------|
| FleetLedger.Infrastructure | Microsoft.EntityFrameworkCore | 10.0.7 |
| FleetLedger.Infrastructure | Microsoft.EntityFrameworkCore.Design | 10.0.7 |
| FleetLedger.Api | Microsoft.EntityFrameworkCore | 10.0.7 |

---

## Migración Aplicada

```
Migración: 20260426005317_InitialCreate
Tabla: depots
Índices: idx_depots_name_unique (unique en Name)
```

---

## Errores Encontrados y Correcciones

### Error 1: Controladores no aparecen en Swagger

**Problema:** Al levantar la API, solo aparecía el endpoint `/health` en Swagger. Los endpoints de `/depots` no aparecían.

**Causa:** No se habían registrado los controladores en Program.cs.

**Solución:** Agregar en Program.cs:
```csharp
builder.Services.AddControllers();  // Registrar servicios de controladores
// ...
app.MapControllers();  // Mapear los controladores
```

**Archivo corregido:** `src/FleetLedger.Api/Program.cs`

---

### Error 2: Typo en el controlador

**Problema:** Errores de sintaxis en `DepotsController.cs` con `ActionResult<Depot>>` (doble `>`).

**Causa:**	Error de transcripción al escribir el código.

**Solución:** Corregido el archivo `src/FleetLedger.Api/Controllers/DepotsController.cs`.

## Estado del Build

```
Compilación correcta.
    0 Advertencias
    0 Errores

ERROR EN RUNTIME (antes de corregir):
- Los endpoints /depots no aparecían en Swagger
- Solo se mostraba /health
```

---

## Notas Adicionales

- Se agregaron atributos `[ProducesResponseType]` en el controlador para mejor documentación Swagger
- La configuración de Swagger está en `Program.cs` con Swashbuckle
- Los endpoints estarán disponibles después de reiniciar la API con los cambios

---

## Siguiente Paso

**Día 3**: Implementar entidad `Driver` con EF Core (CRUD completo)

Samejante estructura que Depot, con campos específicos:
- `LicenseNumber` (único)
- `LicenseCategory`
- `LicenseExpires`
- `DepotId` (FK opcional a Depot)