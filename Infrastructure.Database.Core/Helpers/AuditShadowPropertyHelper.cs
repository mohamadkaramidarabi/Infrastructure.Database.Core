using Infrastructure.Database.Core.ShadowProperties;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Infrastructure.Database.Core.Helpers;

internal static class AuditShadowPropertyHelper
{
    internal static void ApplyCreatedAudit(EntityEntry entry, DateTime now, Guid? userId, string? userRole)
    {
        var createdAtProperty = entry.Property(ShadowPropertyNames.CreatedAtUtc);
        if (createdAtProperty.CurrentValue is DateTime existingCreatedAt && existingCreatedAt != default)
        {
            return;
        }

        SetShadowProperty(entry, ShadowPropertyNames.CreatedAtUtc, now);
        SetShadowProperty(entry, ShadowPropertyNames.CreatedBy, userId);
        SetShadowProperty(entry, ShadowPropertyNames.CreatedByRole, userRole);
    }

    internal static void ApplyModifiedAudit(EntityEntry entry, DateTime now, Guid? userId, string? userRole)
    {
        SetShadowProperty(entry, ShadowPropertyNames.ModifiedAtUtc, now);
        SetShadowProperty(entry, ShadowPropertyNames.ModifiedBy, userId);
        SetShadowProperty(entry, ShadowPropertyNames.ModifiedByRole, userRole);
    }

    internal static void SoftDeleteEntry(EntityEntry entry, DateTime now, Guid? userId, string? userRole)
    {
        SetShadowProperty(entry, ShadowPropertyNames.IsDeleted, true);
        ApplyModifiedAudit(entry, now, userId, userRole);
    }

    internal static bool IsDeleted(EntityEntry entry) =>
        entry.Property(ShadowPropertyNames.IsDeleted).CurrentValue is true;

    internal static bool TransitionedToDeleted(EntityEntry entry)
    {
        var original = entry.Property(ShadowPropertyNames.IsDeleted).OriginalValue is true;
        var current = entry.Property(ShadowPropertyNames.IsDeleted).CurrentValue is true;
        return !original && current;
    }

    internal static void SetShadowProperty(EntityEntry entry, string propertyName, object? value)
    {
        var property = entry.Property(propertyName);
        property.CurrentValue = value;
        property.IsModified = true;
    }
}
