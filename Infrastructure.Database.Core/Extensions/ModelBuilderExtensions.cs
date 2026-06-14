using System.Reflection;
using Infrastructure.Database.Core.ShadowProperties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Core.Extensions;

public static class ModelBuilderExtensions
{
    private static readonly MethodInfo SetSoftDeleteFilterMethod =
        typeof(ModelBuilderExtensions).GetMethod(
            nameof(SetSoftDeleteFilter),
            BindingFlags.NonPublic | BindingFlags.Static)!;

    public static ModelBuilder ApplyAuditShadowProperties(this ModelBuilder modelBuilder)
    {
        foreach (var clrType in GetEntityClrTypes(modelBuilder))
        {
            ConfigureAuditShadowProperties(modelBuilder.Entity(clrType));
        }

        return modelBuilder;
    }

    public static ModelBuilder ApplySoftDeleteShadowProperties(this ModelBuilder modelBuilder)
    {
        foreach (var clrType in GetEntityClrTypes(modelBuilder))
        {
            ConfigureSoftDeleteShadowProperties(modelBuilder.Entity(clrType));
        }

        return modelBuilder;
    }

    public static ModelBuilder ApplySoftDeleteQueryFilters(this ModelBuilder modelBuilder)
    {
        foreach (var clrType in GetEntityClrTypes(modelBuilder))
        {
            SetSoftDeleteFilterMethod
                .MakeGenericMethod(clrType)
                .Invoke(null, [modelBuilder]);
        }

        return modelBuilder;
    }

    /// <summary>
    /// Applies audit shadow properties, soft-delete shadow properties, and global query filters.
    /// Cascade soft-delete on save follows EF relationship configuration: configure
    /// <see cref="DeleteBehavior.Cascade"/> on relationships that should be soft-deleted with the parent.
    /// </summary>
    public static ModelBuilder ApplyInfrastructureConventions(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyAuditShadowProperties();
        modelBuilder.ApplySoftDeleteShadowProperties();
        modelBuilder.ApplySoftDeleteQueryFilters();
        return modelBuilder;
    }

    private static void ConfigureAuditShadowProperties(EntityTypeBuilder entity)
    {
        entity.Property<DateTime>(ShadowPropertyNames.CreatedAtUtc);
        entity.Property<Guid?>(ShadowPropertyNames.CreatedBy);
        entity.Property<string?>(ShadowPropertyNames.CreatedByRole).HasMaxLength(128);
        entity.Property<DateTime>(ShadowPropertyNames.ModifiedAtUtc);
        entity.Property<Guid?>(ShadowPropertyNames.ModifiedBy);
        entity.Property<string?>(ShadowPropertyNames.ModifiedByRole).HasMaxLength(128);
    }

    private static void ConfigureSoftDeleteShadowProperties(EntityTypeBuilder entity)
    {
        entity.Property<bool>(ShadowPropertyNames.IsDeleted);
    }

    private static void SetSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(
            e => EF.Property<bool>(e, ShadowPropertyNames.IsDeleted) == false);
    }

    private static IEnumerable<Type> GetEntityClrTypes(ModelBuilder modelBuilder)
    {
        return modelBuilder.Model.GetEntityTypes()
            .Where(entityType => entityType.ClrType is { IsAbstract: false } && !entityType.IsOwned() && !entityType.IsKeyless)
            .Select(entityType => entityType.ClrType);
    }
}
