using Infrastructure.Database.Core.Extensions;
using Infrastructure.Database.Core.Interceptors;
using Infrastructure.Database.Core.ShadowProperties;
using Infrastructure.Database.Core.Tests.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Database.Core.Tests;

internal sealed class CascadeTestScope : IAsyncDisposable
{
    private readonly IServiceScope _scope;

    private CascadeTestScope(IServiceScope scope, CascadeTestDbContext context)
    {
        _scope = scope;
        Context = context;
    }

    public CascadeTestDbContext Context { get; }

    public static CascadeTestScope Create(TestContextOptions? options = null)
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

        services.AddDbContext<CascadeTestDbContext>((serviceProvider, builder) =>
        {
            builder.UseInMemoryDatabase(options.DatabaseName ?? Guid.NewGuid().ToString());
            builder.AddInfrastructureDatabaseInterceptors(serviceProvider);
        });

        var rootProvider = services.BuildServiceProvider();
        var scope = rootProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CascadeTestDbContext>();

        return new CascadeTestScope(scope, context);
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

internal static class CascadeTestData
{
    public static async Task<(int ParentId, int ChildId, int GrandchildId, int GreatGrandchildId, int RestrictedChildId)>
        SeedHierarchyAsync(CascadeTestDbContext context)
    {
        var parent = new ParentEntity { Name = "Parent" };
        var child = new ChildEntity { Name = "Child" };
        var grandchild = new GrandchildEntity { Name = "Grandchild" };
        var greatGrandchild = new GreatGrandchildEntity { Name = "GreatGrandchild" };
        var restrictedChild = new RestrictedChildEntity { Name = "Restricted" };

        parent.Children.Add(child);
        child.Grandchildren.Add(grandchild);
        grandchild.GreatGrandchildren.Add(greatGrandchild);
        parent.RestrictedChildren.Add(restrictedChild);

        context.Parents.Add(parent);
        await context.SaveChangesAsync();

        return (parent.Id, child.Id, grandchild.Id, greatGrandchild.Id, restrictedChild.Id);
    }
}
