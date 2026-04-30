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

    public async Task<TrackedProductResponse> ExecuteAsync(string userId, AddProductRequest request)
    {
        var fetcher = _resolver.Resolve(request.Source);
        var fetchedPrice = await fetcher.FetchAsync(request.Url);

        var product = Domain.Entities.TrackedProduct.Create(
            request.ListId, userId, request.Url, request.Source, request.Name, request.TargetPrice);

        product.RecordPrice(fetchedPrice);

        await _repository.CreateAsync(product);

        return ToResponse(product);
    }

    private static TrackedProductResponse ToResponse(Domain.Entities.TrackedProduct p) =>
        new(p.Id, p.ListId, p.Name, p.Url, p.Source, p.TargetPrice, p.CurrentPrice, p.LowestPrice, p.IsActive, p.NextCheckAt);
}
