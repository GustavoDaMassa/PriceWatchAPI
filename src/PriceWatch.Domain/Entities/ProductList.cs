namespace PriceWatch.Domain.Entities;

public class ProductList
{
    public string Id { get; private set; } = default!;
    public string UserId { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ProductList() { }

    public static ProductList Create(string userId, string name, string? description)
    {
        return new ProductList
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static ProductList Restore(
        string id,
        string userId,
        string name,
        string? description,
        DateTime createdAt)
    {
        return new ProductList
        {
            Id = id,
            UserId = userId,
            Name = name,
            Description = description,
            CreatedAt = createdAt
        };
    }

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
    }
}
