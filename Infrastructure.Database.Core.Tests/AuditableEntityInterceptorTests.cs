using Infrastructure.Database.Core.Tests.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Infrastructure.Database.Core.Tests;

public class AuditableEntityInterceptorTests
{
    [Fact]
    public async Task SaveChanges_WhenAddingEntity_ShouldSetAllAuditShadowProperties()
    {
        await using var scope = TestScope.Create(new TestContextOptions
        {
            RegisterUserId = true,
            UserId = TestContextOptions.DefaultUserId,
            RegisterUserRole = true,
            UserRole = "Admin"
        });

        var entity = new TestEntity { Name = "Order-1" };
        scope.Context.TestEntities.Add(entity);
        await scope.Context.SaveChangesAsync();

        (await ShadowPropertyReader.GetCreatedAtUtcAsync<TestEntity>(scope.Context, entity.Id))
            .ShouldBe(TestContextOptions.FixedUtcNow);
        (await ShadowPropertyReader.GetCreatedByAsync<TestEntity>(scope.Context, entity.Id))
            .ShouldBe(TestContextOptions.DefaultUserId);
        (await ShadowPropertyReader.GetCreatedByRoleAsync<TestEntity>(scope.Context, entity.Id))
            .ShouldBe("Admin");
        (await ShadowPropertyReader.GetModifiedAtUtcAsync<TestEntity>(scope.Context, entity.Id))
            .ShouldBe(TestContextOptions.FixedUtcNow);
        (await ShadowPropertyReader.GetModifiedByAsync<TestEntity>(scope.Context, entity.Id))
            .ShouldBe(TestContextOptions.DefaultUserId);
        (await ShadowPropertyReader.GetModifiedByRoleAsync<TestEntity>(scope.Context, entity.Id))
            .ShouldBe("Admin");
        (await ShadowPropertyReader.GetIsDeletedAsync<TestEntity>(scope.Context, entity.Id))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task SaveChanges_WhenAddingEntityWithoutUserInDi_ShouldLeaveUserFieldsNull()
    {
        await using var scope = TestScope.Create();

        var entity = new TestEntity { Name = "Order-2" };
        scope.Context.TestEntities.Add(entity);
        await scope.Context.SaveChangesAsync();

        (await ShadowPropertyReader.GetCreatedByAsync<TestEntity>(scope.Context, entity.Id)).ShouldBeNull();
        (await ShadowPropertyReader.GetCreatedByRoleAsync<TestEntity>(scope.Context, entity.Id)).ShouldBeNull();
        (await ShadowPropertyReader.GetModifiedByAsync<TestEntity>(scope.Context, entity.Id)).ShouldBeNull();
        (await ShadowPropertyReader.GetModifiedByRoleAsync<TestEntity>(scope.Context, entity.Id)).ShouldBeNull();
        (await ShadowPropertyReader.GetCreatedAtUtcAsync<TestEntity>(scope.Context, entity.Id))
            .ShouldBe(TestContextOptions.FixedUtcNow);
        (await ShadowPropertyReader.GetModifiedAtUtcAsync<TestEntity>(scope.Context, entity.Id))
            .ShouldBe(TestContextOptions.FixedUtcNow);
    }

    [Fact]
    public async Task SaveChanges_WhenAddingEntityWithOnlyUserId_ShouldSetUserIdAndLeaveRoleNull()
    {
        await using var scope = TestScope.Create(new TestContextOptions
        {
            RegisterUserId = true,
            UserId = TestContextOptions.DefaultUserId
        });

        var entity = new TestEntity { Name = "Order-3" };
        scope.Context.TestEntities.Add(entity);
        await scope.Context.SaveChangesAsync();

        (await ShadowPropertyReader.GetCreatedByAsync<TestEntity>(scope.Context, entity.Id))
            .ShouldBe(TestContextOptions.DefaultUserId);
        (await ShadowPropertyReader.GetCreatedByRoleAsync<TestEntity>(scope.Context, entity.Id)).ShouldBeNull();
        (await ShadowPropertyReader.GetModifiedByAsync<TestEntity>(scope.Context, entity.Id))
            .ShouldBe(TestContextOptions.DefaultUserId);
        (await ShadowPropertyReader.GetModifiedByRoleAsync<TestEntity>(scope.Context, entity.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task SaveChangesSync_WhenAddingEntity_ShouldSetAuditShadowProperties()
    {
        await using var scope = TestScope.Create(new TestContextOptions
        {
            RegisterUserId = true,
            UserId = TestContextOptions.DefaultUserId,
            RegisterUserRole = true,
            UserRole = "Editor"
        });

        var entity = new TestEntity { Name = "Order-sync" };
        scope.Context.TestEntities.Add(entity);
        scope.Context.SaveChanges();

        (await ShadowPropertyReader.GetCreatedAtUtcAsync<TestEntity>(scope.Context, entity.Id))
            .ShouldBe(TestContextOptions.FixedUtcNow);
        (await ShadowPropertyReader.GetCreatedByRoleAsync<TestEntity>(scope.Context, entity.Id))
            .ShouldBe("Editor");
    }

    [Fact]
    public async Task SaveChanges_WhenUpdatingEntity_ShouldUpdateModifiedFieldsOnly()
    {
        var databaseName = Guid.NewGuid().ToString();
        int entityId;

        await using (var createScope = TestScope.Create(new TestContextOptions
        {
            DatabaseName = databaseName,
            RegisterUserId = true,
            UserId = TestContextOptions.DefaultUserId,
            RegisterUserRole = true,
            UserRole = "Admin"
        }))
        {
            var entity = new TestEntity { Name = "Original" };
            createScope.Context.TestEntities.Add(entity);
            await createScope.Context.SaveChangesAsync();
            entityId = entity.Id;
        }

        await using var updateScope = TestScope.Create(new TestContextOptions
        {
            DatabaseName = databaseName,
            UtcNow = () => TestContextOptions.LaterUtcNow,
            RegisterUserId = true,
            UserId = TestContextOptions.OtherUserId,
            RegisterUserRole = true,
            UserRole = "Manager"
        });

        var existing = await updateScope.Context.TestEntities.SingleAsync();
        existing.Name = "Updated";
        await updateScope.Context.SaveChangesAsync();

        (await ShadowPropertyReader.GetCreatedAtUtcAsync<TestEntity>(updateScope.Context, entityId))
            .ShouldBe(TestContextOptions.FixedUtcNow);
        (await ShadowPropertyReader.GetCreatedByAsync<TestEntity>(updateScope.Context, entityId))
            .ShouldBe(TestContextOptions.DefaultUserId);
        (await ShadowPropertyReader.GetCreatedByRoleAsync<TestEntity>(updateScope.Context, entityId))
            .ShouldBe("Admin");
        (await ShadowPropertyReader.GetModifiedAtUtcAsync<TestEntity>(updateScope.Context, entityId))
            .ShouldBe(TestContextOptions.LaterUtcNow);
        (await ShadowPropertyReader.GetModifiedByAsync<TestEntity>(updateScope.Context, entityId))
            .ShouldBe(TestContextOptions.OtherUserId);
        (await ShadowPropertyReader.GetModifiedByRoleAsync<TestEntity>(updateScope.Context, entityId))
            .ShouldBe("Manager");
    }

    [Fact]
    public async Task SaveChanges_WhenDeletingEntity_ShouldSoftDeleteAndStampModifiedAuditFields()
    {
        var databaseName = Guid.NewGuid().ToString();
        int entityId;

        await using (var createScope = TestScope.Create(new TestContextOptions
        {
            DatabaseName = databaseName,
            RegisterUserId = true,
            UserId = TestContextOptions.DefaultUserId,
            RegisterUserRole = true,
            UserRole = "Admin"
        }))
        {
            createScope.Context.TestEntities.Add(new TestEntity { Name = "ToDelete" });
            await createScope.Context.SaveChangesAsync();
            entityId = createScope.Context.TestEntities.Single().Id;
        }

        await using var deleteScope = TestScope.Create(new TestContextOptions
        {
            DatabaseName = databaseName,
            UtcNow = () => TestContextOptions.LaterUtcNow,
            RegisterUserId = true,
            UserId = TestContextOptions.OtherUserId,
            RegisterUserRole = true,
            UserRole = "Manager"
        });

        var entity = await deleteScope.Context.TestEntities.SingleAsync();
        deleteScope.Context.TestEntities.Remove(entity);
        await deleteScope.Context.SaveChangesAsync();

        (await ShadowPropertyReader.GetIsDeletedAsync<TestEntity>(deleteScope.Context, entityId)).ShouldBeTrue();
        (await ShadowPropertyReader.GetCreatedByAsync<TestEntity>(deleteScope.Context, entityId))
            .ShouldBe(TestContextOptions.DefaultUserId);
        (await ShadowPropertyReader.GetModifiedAtUtcAsync<TestEntity>(deleteScope.Context, entityId))
            .ShouldBe(TestContextOptions.LaterUtcNow);
        (await ShadowPropertyReader.GetModifiedByAsync<TestEntity>(deleteScope.Context, entityId))
            .ShouldBe(TestContextOptions.OtherUserId);
        (await ShadowPropertyReader.GetModifiedByRoleAsync<TestEntity>(deleteScope.Context, entityId))
            .ShouldBe("Manager");
    }

    [Fact]
    public async Task SaveChanges_WhenDeletingEntity_ShouldNotPhysicallyRemoveRow()
    {
        var databaseName = Guid.NewGuid().ToString();

        await using (var createScope = TestScope.Create(new TestContextOptions { DatabaseName = databaseName }))
        {
            createScope.Context.TestEntities.Add(new TestEntity { Name = "Persist" });
            await createScope.Context.SaveChangesAsync();
        }

        await using var deleteScope = TestScope.Create(new TestContextOptions { DatabaseName = databaseName });

        var entity = await deleteScope.Context.TestEntities.SingleAsync();
        deleteScope.Context.TestEntities.Remove(entity);
        await deleteScope.Context.SaveChangesAsync();

        (await deleteScope.Context.TestEntities.CountAsync()).ShouldBe(0);
        (await deleteScope.Context.TestEntities.IgnoreQueryFilters().CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Query_ShouldExcludeSoftDeletedEntities()
    {
        var databaseName = Guid.NewGuid().ToString();

        await using (var createScope = TestScope.Create(new TestContextOptions { DatabaseName = databaseName }))
        {
            createScope.Context.TestEntities.AddRange(
                new TestEntity { Name = "Active-1" },
                new TestEntity { Name = "Active-2" });
            await createScope.Context.SaveChangesAsync();
        }

        await using var deleteScope = TestScope.Create(new TestContextOptions { DatabaseName = databaseName });

        var toDelete = await deleteScope.Context.TestEntities
            .OrderBy(entity => entity.Name)
            .FirstAsync();
        deleteScope.Context.TestEntities.Remove(toDelete);
        await deleteScope.Context.SaveChangesAsync();

        var visible = await deleteScope.Context.TestEntities
            .OrderBy(entity => entity.Name)
            .Select(entity => entity.Name)
            .ToListAsync();

        visible.Count.ShouldBe(1);
        visible.Single().ShouldBe("Active-2");
    }

    [Fact]
    public async Task Query_WithIgnoreQueryFilters_ShouldIncludeSoftDeletedEntities()
    {
        var databaseName = Guid.NewGuid().ToString();

        await using (var createScope = TestScope.Create(new TestContextOptions { DatabaseName = databaseName }))
        {
            createScope.Context.TestEntities.Add(new TestEntity { Name = "Deleted" });
            await createScope.Context.SaveChangesAsync();
        }

        await using var deleteScope = TestScope.Create(new TestContextOptions { DatabaseName = databaseName });

        var entity = await deleteScope.Context.TestEntities.SingleAsync();
        deleteScope.Context.TestEntities.Remove(entity);
        await deleteScope.Context.SaveChangesAsync();

        (await deleteScope.Context.TestEntities.IgnoreQueryFilters().CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task SaveChanges_WhenCreatedAtAlreadySet_ShouldNotOverwriteCreatedFields()
    {
        var existingCreatedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var existingCreatedBy = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        await using var scope = TestScope.Create(new TestContextOptions
        {
            RegisterUserId = true,
            UserId = TestContextOptions.DefaultUserId,
            RegisterUserRole = true,
            UserRole = "Admin"
        });

        var entity = new TestEntity { Name = "Prestamped" };
        scope.Context.TestEntities.Add(entity);
        ShadowPropertyReader.SetCreatedAtUtc(scope.Context, entity, existingCreatedAt);
        ShadowPropertyReader.SetCreatedBy(scope.Context, entity, existingCreatedBy);
        await scope.Context.SaveChangesAsync();

        (await ShadowPropertyReader.GetCreatedAtUtcAsync<TestEntity>(scope.Context, entity.Id))
            .ShouldBe(existingCreatedAt);
        (await ShadowPropertyReader.GetCreatedByAsync<TestEntity>(scope.Context, entity.Id))
            .ShouldBe(existingCreatedBy);
        (await ShadowPropertyReader.GetModifiedAtUtcAsync<TestEntity>(scope.Context, entity.Id))
            .ShouldBe(TestContextOptions.FixedUtcNow);
        (await ShadowPropertyReader.GetModifiedByAsync<TestEntity>(scope.Context, entity.Id))
            .ShouldBe(TestContextOptions.DefaultUserId);
    }

    [Fact]
    public async Task SaveChanges_WhenSavingMultipleEntities_ShouldAuditEachEntity()
    {
        await using var scope = TestScope.Create(new TestContextOptions
        {
            RegisterUserId = true,
            UserId = TestContextOptions.DefaultUserId,
            RegisterUserRole = true,
            UserRole = "Admin"
        });

        var first = new TestEntity { Name = "First" };
        var second = new OtherEntity { Code = "Second" };
        scope.Context.TestEntities.Add(first);
        scope.Context.OtherEntities.Add(second);
        await scope.Context.SaveChangesAsync();

        (await ShadowPropertyReader.GetCreatedByAsync<TestEntity>(scope.Context, first.Id))
            .ShouldBe(TestContextOptions.DefaultUserId);
        (await ShadowPropertyReader.GetCreatedByAsync<OtherEntity>(scope.Context, second.Id))
            .ShouldBe(TestContextOptions.DefaultUserId);
        (await ShadowPropertyReader.GetIsDeletedAsync<TestEntity>(scope.Context, first.Id)).ShouldBeFalse();
        (await ShadowPropertyReader.GetIsDeletedAsync<OtherEntity>(scope.Context, second.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task SaveChanges_WhenAddingEntity_IsDeletedShouldRemainFalse()
    {
        await using var scope = TestScope.Create();

        var entity = new TestEntity { Name = "New" };
        scope.Context.TestEntities.Add(entity);
        await scope.Context.SaveChangesAsync();

        (await ShadowPropertyReader.GetIsDeletedAsync<TestEntity>(scope.Context, entity.Id)).ShouldBeFalse();
    }
}
