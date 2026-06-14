using Infrastructure.Database.Core.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Database.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureDatabaseCore(this IServiceCollection services)
    {
        services.AddScoped<AuditableEntityInterceptor>();
        return services;
    }

    public static DbContextOptionsBuilder AddInfrastructureDatabaseInterceptors(
        this DbContextOptionsBuilder optionsBuilder,
        IServiceProvider serviceProvider)
    {
        optionsBuilder.AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>());
        return optionsBuilder;
    }
}
