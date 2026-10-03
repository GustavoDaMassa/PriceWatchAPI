using PriceWatch.Domain.Entities;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

public static class ProductMappings
{
    public static ProductDocument ToDocument(Product product) => new()
    {
        Id = product.Id,
        Gtin = product.Gtin,
        Name = product.Name,
        Brand = product.Brand,
        Model = product.Model,
        ImageUrl = product.ImageUrl,
        MatchStatus = product.MatchStatus,
        SuggestedMatchProductId = product.SuggestedMatchProductId,
        CreatedAt = product.CreatedAt
    };

    public static Product ToDomain(ProductDocument doc) => Product.Restore(
        doc.Id,
        doc.Gtin,
        doc.Name,
        doc.Brand,
        doc.Model,
        doc.ImageUrl,
        doc.MatchStatus,
        doc.SuggestedMatchProductId,
        doc.CreatedAt);
}
