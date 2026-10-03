using PriceWatch.Application.DTOs.Offer;
using PriceWatch.Domain.Services;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.Offers;

public class GetProductComparisonUseCase
{
    private readonly IProductRepository _products;
    private readonly IStoreOfferRepository _offers;

    public GetProductComparisonUseCase(IProductRepository products, IStoreOfferRepository offers)
    {
        _products = products;
        _offers = offers;
    }

    public async Task<ProductComparisonResponse> ExecuteAsync(string productId)
    {
        var product = await _products.GetByIdAsync(productId)
            ?? throw new ProductNotFoundException(productId);

        var offers = (await _offers.GetByProductIdAsync(productId))
            .Where(o => o.IsActive)
            .OrderBy(o => o.CurrentPrice)
            .ToList();

        var cheapestPrice = offers.Count > 0 ? offers.Min(o => o.CurrentPrice) : (decimal?)null;

        var summaries = offers
            .Select(o => new StoreOfferSummary(
                o.Id, o.Source, o.CurrentPrice, o.CurrentPrice == cheapestPrice,
                StoreName(o.Source), o.LowestPrice, o.LastCheckedAt))
            .ToList();

        return new ProductComparisonResponse(product.Id, product.Name, product.ImageUrl, summaries);
    }

    private static string StoreName(Domain.Enums.ProductSource source) =>
        StoreCatalog.All.FirstOrDefault(s => s.Source == source)?.Name ?? source.ToString();
}
