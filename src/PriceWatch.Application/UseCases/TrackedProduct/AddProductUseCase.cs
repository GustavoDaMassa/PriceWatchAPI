using PriceWatch.Application.DTOs.TrackedProduct;
using PriceWatch.Application.Interfaces;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.TrackedProduct;

public class AddProductUseCase
{
    private readonly ITrackedProductRepository _repository;
    private readonly IPriceFetcherResolver _resolver;

    public AddProductUseCase(ITrackedProductRepository repository, IPriceFetcherResolver resolver)
    {
        _repository = repository;
        _resolver = resolver;
    }

    public async Task<TrackedProductResponse> ExecuteAsync(string userId, string listId, AddProductRequest request)
    {
        var fetcher = _resolver.Resolve(request.Url);
        var fetchResult = await fetcher.FetchAsync(request.Url);

        var product = Domain.Entities.TrackedProduct.Create(
            listId, userId, request.Url, fetcher.ProductSource, fetchResult.Name, request.TargetPrice, fetchResult.ImageUrl);

        product.RecordPrice(fetchResult.Price);

        await _repository.CreateAsync(product);

        return ToResponse(product);
    }

    private static TrackedProductResponse ToResponse(Domain.Entities.TrackedProduct p) =>
        new(p.Id, p.ListId, p.Name, p.Url, p.ImageUrl, p.Source, p.TargetPrice, p.CurrentPrice, p.LowestPrice, p.IsActive, p.NextCheckAt);
}
