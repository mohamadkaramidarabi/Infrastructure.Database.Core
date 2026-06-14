using Infrastructure.Database.Core.Tests.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Infrastructure.Database.Core.Tests;

public class CascadeSoftDeleteTests
{
    [Fact]
    public async Task RemoveParent_ShouldCascadeSoftDeleteEntireSubtreeExceptRestricted()
    {
        var databaseName = Guid.NewGuid().ToString();
        int parentId, childId, grandchildId, greatGrandchildId, restrictedChildId;

        await using (var seedScope = CascadeTestScope.Create(new TestContextOptions { DatabaseName = databaseName }))
        {
            var ids = await CascadeTestData.SeedHierarchyAsync(seedScope.Context);
            parentId = ids.ParentId;
            childId = ids.ChildId;
            grandchildId = ids.GrandchildId;
            greatGrandchildId = ids.GreatGrandchildId;
            restrictedChildId = ids.RestrictedChildId;
        }

        await using var deleteScope = CascadeTestScope.Create(new TestContextOptions
        {
            DatabaseName = databaseName,
            UtcNow = () => TestContextOptions.LaterUtcNow,
            RegisterUserId = true,
            UserId = TestContextOptions.OtherUserId,
            RegisterUserRole = true,
            UserRole = "Manager"
        });

        var parent = await deleteScope.Context.Parents.SingleAsync();
        deleteScope.Context.Parents.Remove(parent);
        await deleteScope.Context.SaveChangesAsync();

        (await ShadowPropertyReader.GetIsDeletedAsync<ParentEntity>(deleteScope.Context, parentId)).ShouldBeTrue();
        (await ShadowPropertyReader.GetIsDeletedAsync<ChildEntity>(deleteScope.Context, childId)).ShouldBeTrue();
        (await ShadowPropertyReader.GetIsDeletedAsync<GrandchildEntity>(deleteScope.Context, grandchildId)).ShouldBeTrue();
        (await ShadowPropertyReader.GetIsDeletedAsync<GreatGrandchildEntity>(deleteScope.Context, greatGrandchildId)).ShouldBeTrue();
        (await ShadowPropertyReader.GetIsDeletedAsync<RestrictedChildEntity>(deleteScope.Context, restrictedChildId))
            .ShouldBeFalse();

        (await ShadowPropertyReader.GetModifiedAtUtcAsync<ChildEntity>(deleteScope.Context, childId))
            .ShouldBe(TestContextOptions.LaterUtcNow);
        (await ShadowPropertyReader.GetModifiedByAsync<GrandchildEntity>(deleteScope.Context, grandchildId))
            .ShouldBe(TestContextOptions.OtherUserId);
        (await ShadowPropertyReader.GetModifiedByRoleAsync<GreatGrandchildEntity>(deleteScope.Context, greatGrandchildId))
            .ShouldBe("Manager");
    }

    [Fact]
    public async Task ExplicitSoftDeleteParent_ShouldCascadeSoftDeleteEntireSubtree()
    {
        var databaseName = Guid.NewGuid().ToString();
        int parentId, childId, grandchildId, greatGrandchildId;

        await using (var seedScope = CascadeTestScope.Create(new TestContextOptions { DatabaseName = databaseName }))
        {
            var ids = await CascadeTestData.SeedHierarchyAsync(seedScope.Context);
            parentId = ids.ParentId;
            childId = ids.ChildId;
            grandchildId = ids.GrandchildId;
            greatGrandchildId = ids.GreatGrandchildId;
        }

        await using var deleteScope = CascadeTestScope.Create(new TestContextOptions { DatabaseName = databaseName });

        var parent = await deleteScope.Context.Parents.SingleAsync();
        ShadowPropertyReader.SetIsDeleted(deleteScope.Context, parent, true);
        await deleteScope.Context.SaveChangesAsync();

        (await ShadowPropertyReader.GetIsDeletedAsync<ParentEntity>(deleteScope.Context, parentId)).ShouldBeTrue();
        (await ShadowPropertyReader.GetIsDeletedAsync<ChildEntity>(deleteScope.Context, childId)).ShouldBeTrue();
        (await ShadowPropertyReader.GetIsDeletedAsync<GrandchildEntity>(deleteScope.Context, grandchildId)).ShouldBeTrue();
        (await ShadowPropertyReader.GetIsDeletedAsync<GreatGrandchildEntity>(deleteScope.Context, greatGrandchildId)).ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveMiddleNode_ShouldCascadeOnlyDescendants()
    {
        var databaseName = Guid.NewGuid().ToString();
        int parentId, childId, grandchildId, greatGrandchildId;

        await using (var seedScope = CascadeTestScope.Create(new TestContextOptions { DatabaseName = databaseName }))
        {
            var ids = await CascadeTestData.SeedHierarchyAsync(seedScope.Context);
            parentId = ids.ParentId;
            childId = ids.ChildId;
            grandchildId = ids.GrandchildId;
            greatGrandchildId = ids.GreatGrandchildId;
        }

        await using var deleteScope = CascadeTestScope.Create(new TestContextOptions { DatabaseName = databaseName });

        var child = await deleteScope.Context.Children.SingleAsync();
        deleteScope.Context.Children.Remove(child);
        await deleteScope.Context.SaveChangesAsync();

        (await ShadowPropertyReader.GetIsDeletedAsync<ParentEntity>(deleteScope.Context, parentId)).ShouldBeFalse();
        (await ShadowPropertyReader.GetIsDeletedAsync<ChildEntity>(deleteScope.Context, childId)).ShouldBeTrue();
        (await ShadowPropertyReader.GetIsDeletedAsync<GrandchildEntity>(deleteScope.Context, grandchildId)).ShouldBeTrue();
        (await ShadowPropertyReader.GetIsDeletedAsync<GreatGrandchildEntity>(deleteScope.Context, greatGrandchildId)).ShouldBeTrue();
    }

    [Fact]
    public async Task UndeleteParent_ShouldNotRestoreCascadedDescendants()
    {
        var databaseName = Guid.NewGuid().ToString();
        int parentId, childId;

        await using (var seedScope = CascadeTestScope.Create(new TestContextOptions { DatabaseName = databaseName }))
        {
            var ids = await CascadeTestData.SeedHierarchyAsync(seedScope.Context);
            parentId = ids.ParentId;
            childId = ids.ChildId;
        }

        await using (var deleteScope = CascadeTestScope.Create(new TestContextOptions { DatabaseName = databaseName }))
        {
            var parentToDelete = await deleteScope.Context.Parents.SingleAsync();
            deleteScope.Context.Parents.Remove(parentToDelete);
            await deleteScope.Context.SaveChangesAsync();
        }

        await using var restoreScope = CascadeTestScope.Create(new TestContextOptions { DatabaseName = databaseName });

        var parent = await restoreScope.Context.Parents.IgnoreQueryFilters().SingleAsync();
        ShadowPropertyReader.SetIsDeleted(restoreScope.Context, parent, false);
        await restoreScope.Context.SaveChangesAsync();

        (await ShadowPropertyReader.GetIsDeletedAsync<ParentEntity>(restoreScope.Context, parentId)).ShouldBeFalse();
        (await ShadowPropertyReader.GetIsDeletedAsync<ChildEntity>(restoreScope.Context, childId)).ShouldBeTrue();
    }
}
