using Infrastructure.Database.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Infrastructure.Database.Core.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInfrastructureDatabaseCore_ShouldRegisterAuditableEntityInterceptor()
    {
        var services = new ServiceCollection();

        services.AddInfrastructureDatabaseCore();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetService<Infrastructure.Database.Core.Interceptors.AuditableEntityInterceptor>()
            .ShouldNotBeNull();
    }
}
