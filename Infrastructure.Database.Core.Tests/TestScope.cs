using Infrastructure.Database.Core.Extensions;
using Infrastructure.Database.Core.Interceptors;
using Infrastructure.Database.Core.ShadowProperties;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Database.Core.Tests;

internal sealed class TestScope : IAsyncDisposable
{
    private readonly IServiceScope _scope;

    private TestScope(IServiceScope scope, TestDbContext context)
    {
        _scope = scope;
        Context = context;
    }

    public TestDbContext Context { get; }

    public static TestScope Create(TestContextOptions? options = null)
    {
        options ??= new TestContextOptions();

        var services = new ServiceCollection();
        services.AddInfrastructureDatabaseCore();

        ReplaceInterceptor(services, options.UtcNow);

        if (options.RegisterUserId)
        {
            services.AddKeyedScoped(ServiceKeys.UserId, (_, _) => (object)options.UserId!.Value);
        }

        if (options.RegisterUserRole)
        {
            services.AddKeyedScoped<string>(ServiceKeys.UserRole, (_, _) => options.UserRole!);
        }

        services.AddDbContext<TestDbContext>((serviceProvider, builder) =>
        {
            builder.UseInMemoryDatabase(options.DatabaseName ?? Guid.NewGuid().ToString());
            builder.AddInfrastructureDatabaseInterceptors(serviceProvider);
        });

        var rootProvider = services.BuildServiceProvider();
        var scope = rootProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        return new TestScope(scope, context);
    }

    public ValueTask DisposeAsync()
    {
        Context.Dispose();
        _scope.Dispose();
        return ValueTask.CompletedTask;
    }

    private static void ReplaceInterceptor(IServiceCollection services, Func<DateTime> utcNow)
    {
        var descriptor = services.Single(d => d.ServiceType == typeof(AuditableEntityInterceptor));
        services.Remove(descriptor);
        services.AddScoped<AuditableEntityInterceptor>(serviceProvider =>
            new AuditableEntityInterceptor(serviceProvider, utcNow));
    }
}

internal sealed class TestContextOptions
{
    public string? DatabaseName { get; init; }
    public Func<DateTime> UtcNow { get; init; } = () => FixedUtcNow;
    public bool RegisterUserId { get; init; }
    public Guid? UserId { get; init; }
    public bool RegisterUserRole { get; init; }
    public string? UserRole { get; init; }

    public static DateTime FixedUtcNow { get; } = new(2024, 6, 12, 14, 30, 0, DateTimeKind.Utc);
    public static DateTime LaterUtcNow { get; } = new(2024, 6, 13, 9, 15, 0, DateTimeKind.Utc);

    public static Guid DefaultUserId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static Guid OtherUserId { get; } = Guid.Parse("22222222-2222-2222-2222-222222222222");
}

internal static class ShadowPropertyReader
{
    public static async Task<DateTime> GetCreatedAtUtcAsync<T>(DbContext context, int id)
        where T : class =>
        await ReadShadowPropertyAsync<T, DateTime>(context, id, ShadowPropertyNames.CreatedAtUtc);

    public static async Task<Guid?> GetCreatedByAsync<T>(DbContext context, int id)
        where T : class =>
        await ReadShadowPropertyAsync<T, Guid?>(context, id, ShadowPropertyNames.CreatedBy);

    public static async Task<string?> GetCreatedByRoleAsync<T>(DbContext context, int id)
        where T : class =>
        await ReadShadowPropertyAsync<T, string?>(context, id, ShadowPropertyNames.CreatedByRole);

    public static async Task<DateTime> GetModifiedAtUtcAsync<T>(DbContext context, int id)
        where T : class =>
        await ReadShadowPropertyAsync<T, DateTime>(context, id, ShadowPropertyNames.ModifiedAtUtc);

    public static async Task<Guid?> GetModifiedByAsync<T>(DbContext context, int id)
        where T : class =>
        await ReadShadowPropertyAsync<T, Guid?>(context, id, ShadowPropertyNames.ModifiedBy);

    public static async Task<string?> GetModifiedByRoleAsync<T>(DbContext context, int id)
        where T : class =>
        await ReadShadowPropertyAsync<T, string?>(context, id, ShadowPropertyNames.ModifiedByRole);

    public static async Task<bool> GetIsDeletedAsync<T>(DbContext context, int id)
        where T : class =>
        await ReadShadowPropertyAsync<T, bool>(context, id, ShadowPropertyNames.IsDeleted);

    public static void SetCreatedAtUtc(DbContext context, object entity, DateTime value) =>
        context.Entry(entity).Property(ShadowPropertyNames.CreatedAtUtc).CurrentValue = value;

    public static void SetCreatedBy(DbContext context, object entity, Guid? value) =>
        context.Entry(entity).Property(ShadowPropertyNames.CreatedBy).CurrentValue = value;

    public static void SetIsDeleted(DbContext context, object entity, bool value)
    {
        var property = context.Entry(entity).Property(ShadowPropertyNames.IsDeleted);
        property.CurrentValue = value;
        property.IsModified = true;
    }

    private static async Task<TValue> ReadShadowPropertyAsync<TEntity, TValue>(
        DbContext context,
        int id,
        string propertyName)
        where TEntity : class
    {
        var trackedEntry = context.ChangeTracker.Entries<TEntity>()
            .FirstOrDefault(entry => entry.Property("Id").CurrentValue is int entityId && entityId == id);

        if (trackedEntry is not null)
        {
            return (TValue)trackedEntry.Property(propertyName).CurrentValue!;
        }

        context.ChangeTracker.Clear();

        var entity = await context.Set<TEntity>()
            .IgnoreQueryFilters()
            .FirstAsync(e => EF.Property<int>(e, "Id") == id);

        var entry = context.Entry(entity);
        await entry.ReloadAsync();

        return (TValue)entry.Property(propertyName).CurrentValue!;
    }
}
