# Infrastructure.Database.Core

A small EF Core companion library that adds **audit fields**, **soft delete**, and **cascade soft delete** to your entities using **shadow properties** — no marker interfaces or base classes on your domain models.

Built for **.NET 10** and **EF Core 10**.

## Features

- **Automatic audit stamping** on `SaveChanges` — created/modified timestamps and user context
- **Soft delete** — `Remove()` sets `is_deleted = true` instead of physically deleting rows
- **Global query filters** — deleted rows are excluded from queries by default
- **Cascade soft delete** — recursively soft-deletes dependents along FK relationships configured with `DeleteBehavior.Cascade`
- **Filtered includes** — `Include()` navigations respect each entity's soft-delete filter
- **Plain POCO entities** — all infrastructure columns are shadow properties configured in `OnModelCreating`

## Installation

Reference the project or package in your application:

```xml
<ProjectReference Include="..\Infrastructure.Database.Core\Infrastructure.Database.Core.csproj" />
```

The library depends on:

- `Microsoft.EntityFrameworkCore` (10.0.9)
- `Microsoft.Extensions.DependencyInjection` (10.0.9)

## Quick start

### 1. Register services

```csharp
using Infrastructure.Database.Core;
using Infrastructure.Database.Core.Extensions;

services.AddInfrastructureDatabaseCore();

// Optional: current user context (resolved per scope at save time)
services.AddKeyedScoped(ServiceKeys.UserId, (_, _) => (object)currentUserId);
services.AddKeyedScoped<string>(ServiceKeys.UserRole, (_, _) => currentUserRole);
```

Register `user_id` as `(object)guid` so the interceptor can resolve it via `GetKeyedService<object>` and unbox the `Guid`.

### 2. Configure DbContext

```csharp
services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer(connectionString);
    options.AddInfrastructureDatabaseInterceptors(serviceProvider);
});
```

The interceptor is **scoped**. Always register it through `AddInfrastructureDatabaseInterceptors` inside the `AddDbContext` callback so it resolves from the same scope as the context.

### 3. Apply model conventions

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Your entity relationships...
        modelBuilder.ApplyInfrastructureConventions();
    }
}
```

Your entities stay simple:

```csharp
public class Order
{
    public int Id { get; set; }
    public decimal Total { get; set; }
}
```

After the next migration, EF will add the shadow columns to the database schema.

## Shadow properties

Conventions add these shadow columns to every root entity (non-owned, non-keyless):

| Property | Type | Set on |
|----------|------|--------|
| `created_at_utc` | `DateTime` | Insert (only if not already set) |
| `created_by` | `Guid?` | Insert |
| `created_by_role` | `string?` (max 128) | Insert |
| `modified_at_utc` | `DateTime` | Insert and update |
| `modified_by` | `Guid?` | Insert and update |
| `modified_by_role` | `string?` (max 128) | Insert and update |
| `is_deleted` | `bool` | Soft delete |

If no user is registered in DI, `created_by` / `modified_by` and role fields are left `null` (for example, background jobs or system writes).

## User context

The interceptor reads the current user from **keyed scoped services** at save time:

| Key constant | DI key | Type |
|--------------|--------|------|
| `ServiceKeys.UserId` | `"user_id"` | `Guid?` (register as `(object)guid`) |
| `ServiceKeys.UserRole` | `"user_role"` | `string?` |

Typical ASP.NET Core wiring:

```csharp
services.AddKeyedScoped(ServiceKeys.UserId, (sp, _) =>
{
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    // Resolve your user id from claims, etc.
    return (object?)userId;
});

services.AddKeyedScoped<string>(ServiceKeys.UserRole, (sp, _) => userRole);
```

## Soft delete

Hard deletes are intercepted and converted to updates:

```csharp
context.Orders.Remove(order);
await context.SaveChangesAsync();
// Row remains in the database with is_deleted = true
```

Default queries exclude soft-deleted rows:

```csharp
var activeOrders = await context.Orders.ToListAsync();
```

To include deleted rows:

```csharp
var allOrders = await context.Orders.IgnoreQueryFilters().ToListAsync();
```

**Note:** `IgnoreQueryFilters()` applies to the entire query, including `Include()` navigations. Soft-deleted related rows will appear in included collections when filters are ignored.

## Cascade soft delete

When an entity transitions to `is_deleted = true` (via `Remove()` or an explicit update), the library recursively soft-deletes dependents along foreign keys configured with **`DeleteBehavior.Cascade`**.

```csharp
modelBuilder.Entity<OrderLine>()
    .HasOne(line => line.Order)
    .WithMany(order => order.Lines)
    .HasForeignKey(line => line.OrderId)
    .OnDelete(DeleteBehavior.Cascade);
```

Deleting an `Order` soft-deletes all related `OrderLine` rows in the **same** `SaveChanges` call, including deeper levels (grandchildren, and so on) when those relationships also use cascade.

### Cascade rules

| Behavior | Result |
|----------|--------|
| `DeleteBehavior.Cascade` | Dependents are soft-deleted recursively |
| `DeleteBehavior.Restrict`, `SetNull`, etc. | Not affected |
| Un-deleting a parent (`is_deleted = false`) | Does **not** restore cascaded descendants |
| Database `ON DELETE CASCADE` | Not used — this is logical soft delete in the interceptor; avoid real cascade deletes in migrations |

## Includes and query filters

Global filters are applied to every root entity. EF Core applies each entity's filter inside `Include` subqueries automatically:

```csharp
var order = await context.Orders
    .Include(o => o.Lines)
    .FirstAsync(o => o.Id == id);

// Lines collection excludes is_deleted == true rows
```

Multi-level includes filter at each navigation level:

```csharp
var order = await context.Orders
    .Include(o => o.Lines)
        .ThenInclude(line => line.Details)
    .FirstAsync(o => o.Id == id);
```

## Public API

### `ServiceCollectionExtensions`

| Method | Description |
|--------|-------------|
| `AddInfrastructureDatabaseCore()` | Registers the scoped `AuditableEntityInterceptor` |
| `AddInfrastructureDatabaseInterceptors(optionsBuilder, serviceProvider)` | Adds the interceptor to `DbContextOptions` |

### `ModelBuilderExtensions`

| Method | Description |
|--------|-------------|
| `ApplyInfrastructureConventions()` | Audit + soft-delete shadow props + query filters (recommended) |
| `ApplyAuditShadowProperties()` | Audit shadow properties only |
| `ApplySoftDeleteShadowProperties()` | `is_deleted` shadow property only |
| `ApplySoftDeleteQueryFilters()` | Global `is_deleted == false` filter only |

### `ServiceKeys`

| Constant | Value |
|----------|-------|
| `ServiceKeys.UserId` | `"user_id"` |
| `ServiceKeys.UserRole` | `"user_role"` |

## Design notes

- **No entity interfaces** — conventions target all root entity types discovered in the model.
- **Owned and keyless types** are skipped (same rules as convention application).
- **Created timestamps** are not overwritten if already set before save (supports data import or backfill scenarios).
- **Scoped interceptor** — required for keyed DI user resolution; do not register the interceptor as a singleton.

## Testing

See `Infrastructure.Database.Core.Tests` for InMemory integration tests covering audit stamping, soft delete, cascade behavior, and filtered includes.

```bash
dotnet test Infrastructure.Database.Core.Tests
```
