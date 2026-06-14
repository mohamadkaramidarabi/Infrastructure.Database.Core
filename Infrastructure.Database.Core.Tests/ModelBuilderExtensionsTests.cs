using Infrastructure.Database.Core.Extensions;
using Infrastructure.Database.Core.ShadowProperties;
using Infrastructure.Database.Core.Tests.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Infrastructure.Database.Core.Tests;

public class ModelBuilderExtensionsTests
{
    [Fact]
    public void ApplyInfrastructureConventions_ShouldConfigureAllShadowProperties()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.ApplyInfrastructureConventions();

        var entityType = modelBuilder.Model.FindEntityType(typeof(TestEntity));
        entityType.ShouldNotBeNull();

        AssertShadowProperty(entityType, ShadowPropertyNames.CreatedAtUtc, typeof(DateTime));
        AssertShadowProperty(entityType, ShadowPropertyNames.CreatedBy, typeof(Guid?));
        AssertShadowProperty(entityType, ShadowPropertyNames.CreatedByRole, typeof(string));
        AssertShadowProperty(entityType, ShadowPropertyNames.ModifiedAtUtc, typeof(DateTime));
        AssertShadowProperty(entityType, ShadowPropertyNames.ModifiedBy, typeof(Guid?));
        AssertShadowProperty(entityType, ShadowPropertyNames.ModifiedByRole, typeof(string));
        AssertShadowProperty(entityType, ShadowPropertyNames.IsDeleted, typeof(bool));
    }

    [Fact]
    public void ApplyAuditShadowProperties_ShouldConfigureOnlyAuditShadowProperties()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.ApplyAuditShadowProperties();

        var entityType = modelBuilder.Model.FindEntityType(typeof(TestEntity))!;

        AssertShadowProperty(entityType, ShadowPropertyNames.CreatedAtUtc, typeof(DateTime));
        AssertShadowProperty(entityType, ShadowPropertyNames.CreatedBy, typeof(Guid?));
        AssertShadowProperty(entityType, ShadowPropertyNames.CreatedByRole, typeof(string));
        AssertShadowProperty(entityType, ShadowPropertyNames.ModifiedAtUtc, typeof(DateTime));
        AssertShadowProperty(entityType, ShadowPropertyNames.ModifiedBy, typeof(Guid?));
        AssertShadowProperty(entityType, ShadowPropertyNames.ModifiedByRole, typeof(string));
        entityType.FindProperty(ShadowPropertyNames.IsDeleted).ShouldBeNull();
    }

    [Fact]
    public void ApplySoftDeleteShadowProperties_ShouldConfigureOnlyIsDeleted()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.ApplySoftDeleteShadowProperties();

        var entityType = modelBuilder.Model.FindEntityType(typeof(TestEntity))!;

        AssertShadowProperty(entityType, ShadowPropertyNames.IsDeleted, typeof(bool));
        entityType.FindProperty(ShadowPropertyNames.CreatedAtUtc).ShouldBeNull();
        entityType.FindProperty(ShadowPropertyNames.ModifiedBy).ShouldBeNull();
    }

    [Fact]
    public void ApplySoftDeleteQueryFilters_ShouldConfigureQueryFilter()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<TestEntity>();
        modelBuilder.ApplySoftDeleteShadowProperties();
        modelBuilder.ApplySoftDeleteQueryFilters();

        var entityType = modelBuilder.Model.FindEntityType(typeof(TestEntity))!;
        entityType.GetDeclaredQueryFilters().ShouldNotBeEmpty();
    }

    [Fact]
    public void ApplyInfrastructureConventions_ShouldApplyToAllRootEntities()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<TestEntity>();
        modelBuilder.Entity<OtherEntity>();
        modelBuilder.ApplyInfrastructureConventions();

        modelBuilder.Model.FindEntityType(typeof(TestEntity))!
            .FindProperty(ShadowPropertyNames.CreatedAtUtc)
            .ShouldNotBeNull();

        modelBuilder.Model.FindEntityType(typeof(OtherEntity))!
            .FindProperty(ShadowPropertyNames.IsDeleted)
            .ShouldNotBeNull();
    }

    [Fact]
    public void ApplyInfrastructureConventions_ShouldReturnSameModelBuilderForChaining()
    {
        var modelBuilder = CreateModelBuilder();

        var result = modelBuilder.ApplyInfrastructureConventions();

        result.ShouldBeSameAs(modelBuilder);
    }

    private static ModelBuilder CreateModelBuilder()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<TestEntity>();
        return modelBuilder;
    }

    private static void AssertShadowProperty(
        Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType,
        string propertyName,
        Type expectedClrType)
    {
        var property = entityType.FindProperty(propertyName);
        property.ShouldNotBeNull();
        property!.IsShadowProperty().ShouldBeTrue();
        property.ClrType.ShouldBe(expectedClrType);
    }
}
