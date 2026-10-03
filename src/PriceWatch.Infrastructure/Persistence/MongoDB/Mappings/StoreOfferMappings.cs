using PriceWatch.Domain.Entities;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

public static class StoreOfferMappings
{
    public static StoreOfferDocument ToDocument(StoreOffer offer) => new()
    {
        Id = offer.Id,
        ProductId = offer.ProductId,
        Source = offer.Source,
        Url = offer.Url,
        Gtin = offer.Gtin,
        CurrentPrice = offer.CurrentPrice,
        LowestPrice = offer.LowestPrice,
        AffiliateUrl = offer.AffiliateUrl,
        CheckIntervalHours = offer.CheckIntervalHours,
        NextCheckAt = offer.NextCheckAt,
        LastCheckedAt = offer.LastCheckedAt,
        IsActive = offer.IsActive,
        CreatedAt = offer.CreatedAt
    };

    public static StoreOffer ToDomain(StoreOfferDocument doc) => StoreOffer.Restore(
        doc.Id,
        doc.ProductId,
        doc.Source,
        doc.Url,
        doc.Gtin,
        doc.CurrentPrice,
        doc.LowestPrice,
        doc.AffiliateUrl,
        doc.CheckIntervalHours,
        doc.NextCheckAt,
        doc.LastCheckedAt,
        doc.IsActive,
        doc.CreatedAt);
}
