using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Infrastructure.Database.Core.Helpers;

internal static class CascadeSoftDeleteHelper
{
    internal static void Cascade(
        DbContext context,
        EntityEntry entry,
        DateTime now,
        Guid? userId,
        string? userRole,
        ISet<string> visited)
    {
        if (!TryMarkVisited(entry, visited))
        {
            return;
        }

        foreach (var foreignKey in entry.Metadata.GetReferencingForeignKeys())
        {
            if (foreignKey.DeleteBehavior != DeleteBehavior.Cascade)
            {
                continue;
            }

            if (foreignKey.DeclaringEntityType.IsOwned()
                || foreignKey.DeclaringEntityType.FindPrimaryKey() is null)
            {
                continue;
            }

            foreach (var childEntry in LoadMatchingChildren(context, entry, foreignKey))
            {
                if (AuditShadowPropertyHelper.IsDeleted(childEntry))
                {
                    continue;
                }

                AuditShadowPropertyHelper.SoftDeleteEntry(childEntry, now, userId, userRole);
                Cascade(context, childEntry, now, userId, userRole, visited);
            }
        }
    }

    private static IEnumerable<EntityEntry> LoadMatchingChildren(
        DbContext context,
        EntityEntry parentEntry,
        IForeignKey foreignKey)
    {
        var childClrType = foreignKey.DeclaringEntityType.ClrType;
        var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
            .MakeGenericMethod(childClrType);
        var query = (IQueryable)setMethod.Invoke(context, null)!;

        var ignoreQueryFiltersMethod = typeof(CascadeSoftDeleteHelper)
            .GetMethod(nameof(IgnoreQueryFilters), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(childClrType);
        query = (IQueryable)ignoreQueryFiltersMethod.Invoke(null, [query])!;

        query = ApplyForeignKeyFilter(query, parentEntry, foreignKey);

        foreach (var child in query)
        {
            var childEntry = context.Entry(child);
            if (childEntry.State == EntityState.Detached)
            {
                context.Attach(child);
                childEntry = context.Entry(child);
            }

            yield return childEntry;
        }
    }

    private static IQueryable ApplyForeignKeyFilter(
        IQueryable query,
        EntityEntry parentEntry,
        IForeignKey foreignKey)
    {
        var childClrType = foreignKey.DeclaringEntityType.ClrType;
        var parameter = Expression.Parameter(childClrType, "entity");
        Expression? predicate = null;

        for (var index = 0; index < foreignKey.Properties.Count; index++)
        {
            var foreignKeyProperty = foreignKey.Properties[index];
            var principalKeyProperty = foreignKey.PrincipalKey.Properties[index];
            var principalValue = parentEntry.Property(principalKeyProperty.Name).CurrentValue;

            var propertyMethod = typeof(EF).GetMethod(nameof(EF.Property))!
                .MakeGenericMethod(foreignKeyProperty.ClrType);
            var propertyAccess = Expression.Call(propertyMethod, parameter, Expression.Constant(foreignKeyProperty.Name));
            var constant = Expression.Constant(principalValue, foreignKeyProperty.ClrType);
            var equals = Expression.Equal(propertyAccess, constant);
            predicate = predicate is null ? equals : Expression.AndAlso(predicate, equals);
        }

        if (predicate is null)
        {
            return query;
        }

        var lambda = Expression.Lambda(predicate, parameter);
        var whereMethod = typeof(Queryable).GetMethods()
            .First(method => method.Name == nameof(Queryable.Where) && method.GetParameters().Length == 2)
            .MakeGenericMethod(childClrType);

        return (IQueryable)whereMethod.Invoke(null, [query, lambda])!;
    }

    private static IQueryable<T> IgnoreQueryFilters<T>(IQueryable<T> query)
        where T : class =>
        query.IgnoreQueryFilters();

    private static bool TryMarkVisited(EntityEntry entry, ISet<string> visited)
    {
        var key = BuildVisitKey(entry);
        return visited.Add(key);
    }

    private static string BuildVisitKey(EntityEntry entry)
    {
        var keyProperties = entry.Metadata.FindPrimaryKey()?.Properties;
        if (keyProperties is null || keyProperties.Count == 0)
        {
            return entry.Metadata.ClrType.FullName ?? entry.Metadata.Name;
        }

        var keyValues = keyProperties
            .Select(property => entry.Property(property.Name).CurrentValue?.ToString() ?? "null");
        return $"{entry.Metadata.ClrType.FullName}:{string.Join("|", keyValues)}";
    }
}
