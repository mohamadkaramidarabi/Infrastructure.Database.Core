namespace Infrastructure.Database.Core.Tests.Entities;

public class ParentEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<ChildEntity> Children { get; set; } = [];
    public ICollection<RestrictedChildEntity> RestrictedChildren { get; set; } = [];
}

public class ChildEntity
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ParentEntity Parent { get; set; } = null!;
    public ICollection<GrandchildEntity> Grandchildren { get; set; } = [];
}

public class GrandchildEntity
{
    public int Id { get; set; }
    public int ChildId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ChildEntity Child { get; set; } = null!;
    public ICollection<GreatGrandchildEntity> GreatGrandchildren { get; set; } = [];
}

public class GreatGrandchildEntity
{
    public int Id { get; set; }
    public int GrandchildId { get; set; }
    public string Name { get; set; } = string.Empty;
    public GrandchildEntity Grandchild { get; set; } = null!;
}

public class RestrictedChildEntity
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ParentEntity Parent { get; set; } = null!;
}
