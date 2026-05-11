using PriceWatch.Application.DTOs.TrackedProduct;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.TrackedProduct;

public class GetProductsByListUseCase
{
    private readonly ITrackedProductRepository _repository;

    public GetProductsByListUseCase(ITrackedProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<TrackedProductResponse>> ExecuteAsync(string listId)
    {
        var products = await _repository.GetByListIdAsync(listId);
        return products.Select(p => new TrackedProductResponse(
            p.Id, p.ListId, p.Name, p.Url, p.ImageUrl, p.Source, p.TargetPrice, p.CurrentPrice, p.LowestPrice, p.IsActive, p.NextCheckAt));
    }
}
