using Infrastructure.Database.Core.Extensions;
using Infrastructure.Database.Core.Tests.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.Core.Tests;

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    public DbSet<TestEntity> TestEntities => Set<TestEntity>();
    public DbSet<OtherEntity> OtherEntities => Set<OtherEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyInfrastructureConventions();
    }
}
