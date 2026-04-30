using PriceWatch.Domain.Entities;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

public static class ProductListMappings
{
    public static ProductListDocument ToDocument(ProductList list) => new()
    {
        Id = list.Id,
        UserId = list.UserId,
        Name = list.Name,
        Description = list.Description,
        CreatedAt = list.CreatedAt
    };

    public static ProductList ToDomain(ProductListDocument doc) => ProductList.Restore(
        doc.Id,
        doc.UserId,
        doc.Name,
        doc.Description,
        doc.CreatedAt);
}
