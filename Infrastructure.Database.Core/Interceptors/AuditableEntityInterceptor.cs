using Infrastructure.Database.Core.Helpers;
using Infrastructure.Database.Core.ShadowProperties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Database.Core.Interceptors;

internal sealed class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Func<DateTime> _getUtcNow;

    public AuditableEntityInterceptor(IServiceProvider serviceProvider, Func<DateTime>? getUtcNow = null)
    {
        _serviceProvider = serviceProvider;
        _getUtcNow = getUtcNow ?? (() => DateTime.UtcNow);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            ApplyChanges(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken)
    {
        if (eventData.Context is not null)
        {
            ApplyChanges(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyChanges(DbContext context)
    {
        var now = _getUtcNow();
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();
        var visited = new HashSet<string>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                AuditShadowPropertyHelper.SetShadowProperty(entry, ShadowPropertyNames.IsDeleted, true);
            }

            if (entry.State == EntityState.Added)
            {
                AuditShadowPropertyHelper.ApplyCreatedAudit(entry, now, userId, userRole);
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                AuditShadowPropertyHelper.ApplyModifiedAudit(entry, now, userId, userRole);
            }

            if (AuditShadowPropertyHelper.TransitionedToDeleted(entry))
            {
                CascadeSoftDeleteHelper.Cascade(context, entry, now, userId, userRole, visited);
            }
        }
    }

    private Guid? GetCurrentUserId() => ResolveUserId(_serviceProvider);

    private string? GetCurrentUserRole() =>
        _serviceProvider.GetKeyedService<string>(ServiceKeys.UserRole);
    private static Guid? ResolveUserId(IServiceProvider serviceProvider)
    {
        var value = serviceProvider.GetKeyedService<object>(ServiceKeys.UserId);
        if (value is Guid guid)
        {
            return guid;
        }

        return value as Guid?;
    }
}
