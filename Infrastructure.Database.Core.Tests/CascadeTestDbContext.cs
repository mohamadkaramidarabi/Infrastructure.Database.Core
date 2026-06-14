using Infrastructure.Database.Core.Extensions;
using Infrastructure.Database.Core.Tests.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.Core.Tests;

public class CascadeTestDbContext : DbContext
{
    public CascadeTestDbContext(DbContextOptions<CascadeTestDbContext> options)
        : base(options)
    {
    }

    public DbSet<ParentEntity> Parents => Set<ParentEntity>();
    public DbSet<ChildEntity> Children => Set<ChildEntity>();
    public DbSet<GrandchildEntity> Grandchildren => Set<GrandchildEntity>();
    public DbSet<GreatGrandchildEntity> GreatGrandchildren => Set<GreatGrandchildEntity>();
    public DbSet<RestrictedChildEntity> RestrictedChildren => Set<RestrictedChildEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChildEntity>()
            .HasOne(child => child.Parent)
            .WithMany(parent => parent.Children)
            .HasForeignKey(child => child.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GrandchildEntity>()
            .HasOne(grandchild => grandchild.Child)
            .WithMany(child => child.Grandchildren)
            .HasForeignKey(grandchild => grandchild.ChildId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GreatGrandchildEntity>()
            .HasOne(greatGrandchild => greatGrandchild.Grandchild)
            .WithMany(grandchild => grandchild.GreatGrandchildren)
            .HasForeignKey(greatGrandchild => greatGrandchild.GrandchildId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RestrictedChildEntity>()
            .HasOne(child => child.Parent)
            .WithMany(parent => parent.RestrictedChildren)
            .HasForeignKey(child => child.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.ApplyInfrastructureConventions();
    }
}
