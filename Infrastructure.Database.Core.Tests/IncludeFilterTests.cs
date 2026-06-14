using Infrastructure.Database.Core.Tests.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Infrastructure.Database.Core.Tests;

public class IncludeFilterTests
{
    [Fact]
    public async Task IncludeChildren_ShouldExcludeSoftDeletedChildren()
    {
        var databaseName = Guid.NewGuid().ToString();
        int parentId, activeChildId, deletedChildId;

        await using (var seedScope = CascadeTestScope.Create(new TestContextOptions { DatabaseName = databaseName }))
        {
            var seededParent = new ParentEntity
            {
                Name = "Parent",
                Children =
                [
                    new ChildEntity { Name = "Active" },
                    new ChildEntity { Name = "Deleted" }
                ]
            };
            seedScope.Context.Parents.Add(seededParent);
            await seedScope.Context.SaveChangesAsync();
            parentId = seededParent.Id;
            activeChildId = seededParent.Children.First(child => child.Name == "Active").Id;
            deletedChildId = seededParent.Children.First(child => child.Name == "Deleted").Id;

            var toDelete = await seedScope.Context.Children.SingleAsync(child => child.Id == deletedChildId);
            seedScope.Context.Children.Remove(toDelete);
            await seedScope.Context.SaveChangesAsync();
        }

        await using var readScope = CascadeTestScope.Create(new TestContextOptions { DatabaseName = databaseName });

        var parent = await readScope.Context.Parents
            .Include(p => p.Children)
            .SingleAsync(p => p.Id == parentId);

        parent.Children.Count.ShouldBe(1);
        parent.Children.Single().Id.ShouldBe(activeChildId);
    }

    [Fact]
    public async Task IncludeChain_ShouldExcludeSoftDeletedRowsAtEachLevel()
    {
        var databaseName = Guid.NewGuid().ToString();
        int parentId, activeGrandchildId;

        await using (var seedScope = CascadeTestScope.Create(new TestContextOptions { DatabaseName = databaseName }))
        {
            var seededParent = new ParentEntity
            {
                Name = "Parent",
                Children =
                [
                    new ChildEntity
                    {
                        Name = "Child",
                        Grandchildren =
                        [
                            new GrandchildEntity { Name = "ActiveGrandchild" },
                            new GrandchildEntity { Name = "DeletedGrandchild" }
                        ]
                    }
                ]
            };
            seedScope.Context.Parents.Add(seededParent);
            await seedScope.Context.SaveChangesAsync();
            parentId = seededParent.Id;
            activeGrandchildId = seededParent.Children.Single().Grandchildren
                .First(grandchild => grandchild.Name == "ActiveGrandchild").Id;

            var toDelete = await seedScope.Context.Grandchildren
                .SingleAsync(grandchild => grandchild.Name == "DeletedGrandchild");
            seedScope.Context.Grandchildren.Remove(toDelete);
            await seedScope.Context.SaveChangesAsync();
        }

        await using var readScope = CascadeTestScope.Create(new TestContextOptions { DatabaseName = databaseName });

        var parent = await readScope.Context.Parents
            .Include(p => p.Children)
            .ThenInclude(child => child.Grandchildren)
            .SingleAsync(p => p.Id == parentId);

        var child = parent.Children.Single();
        child.Grandchildren.Count.ShouldBe(1);
        child.Grandchildren.Single().Id.ShouldBe(activeGrandchildId);
    }

    [Fact]
    public async Task SoftDeletedParent_ShouldBeExcludedFromDefaultQuery()
    {
        var databaseName = Guid.NewGuid().ToString();

        await using (var seedScope = CascadeTestScope.Create(new TestContextOptions { DatabaseName = databaseName }))
        {
            seedScope.Context.Parents.Add(new ParentEntity { Name = "DeletedParent" });
            await seedScope.Context.SaveChangesAsync();

            var parent = await seedScope.Context.Parents.SingleAsync();
            seedScope.Context.Parents.Remove(parent);
            await seedScope.Context.SaveChangesAsync();
        }

        await using var readScope = CascadeTestScope.Create(new TestContextOptions { DatabaseName = databaseName });

        (await readScope.Context.Parents.CountAsync()).ShouldBe(0);
        (await readScope.Context.Parents.IgnoreQueryFilters().CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task IgnoreQueryFiltersOnParent_ShouldIncludeSoftDeletedParentAndChildren()
    {
        var databaseName = Guid.NewGuid().ToString();
        int parentId, activeChildId, deletedChildId;

        await using (var seedScope = CascadeTestScope.Create(new TestContextOptions { DatabaseName = databaseName }))
        {
            var seededParent = new ParentEntity
            {
                Name = "Parent",
                Children =
                [
                    new ChildEntity { Name = "Active" },
                    new ChildEntity { Name = "Deleted" }
                ]
            };
            seedScope.Context.Parents.Add(seededParent);
            await seedScope.Context.SaveChangesAsync();
            parentId = seededParent.Id;
            activeChildId = seededParent.Children.First(child => child.Name == "Active").Id;
            deletedChildId = seededParent.Children.First(child => child.Name == "Deleted").Id;

            var toDelete = await seedScope.Context.Children.SingleAsync(child => child.Name == "Deleted");
            seedScope.Context.Children.Remove(toDelete);
            await seedScope.Context.SaveChangesAsync();

            seedScope.Context.Parents.Remove(seededParent);
            await seedScope.Context.SaveChangesAsync();
        }

        await using var readScope = CascadeTestScope.Create(new TestContextOptions { DatabaseName = databaseName });

        var parent = await readScope.Context.Parents
            .IgnoreQueryFilters()
            .Include(p => p.Children)
            .SingleAsync(p => p.Id == parentId);

        parent.Children.Count.ShouldBe(2);
        parent.Children.Select(child => child.Id).ShouldContain(activeChildId);
        parent.Children.Select(child => child.Id).ShouldContain(deletedChildId);
    }
}
