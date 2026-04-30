using PriceWatch.Application.DTOs.ProductList;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.ProductList;

public class GetListAnalysisUseCase
{
    private readonly IProductListRepository _listRepository;
    private readonly ITrackedProductRepository _productRepository;

    public GetListAnalysisUseCase(
        IProductListRepository listRepository,
        ITrackedProductRepository productRepository)
    {
        _listRepository = listRepository;
        _productRepository = productRepository;
    }

    public async Task<IEnumerable<AnalysisItemDto>> ExecuteAsync(string listId, string userId)
    {
        var list = await _listRepository.GetByIdAsync(listId)
            ?? throw new ProductListNotFoundException(listId);

        if (list.UserId != userId)
            throw new ProductListNotFoundException(listId);

        var products = await _productRepository.GetByListIdAsync(listId);

        return products
            .Where(p => p.IsActive)
            .Select(p => new AnalysisItemDto(
                p.Id,
                p.Name,
                p.CurrentPrice,
                p.TargetPrice,
                p.TargetPrice == 0 ? 0 :
                    Math.Round((p.CurrentPrice - p.TargetPrice) / p.TargetPrice * 100, 2)))
            .OrderBy(a => a.DistancePercent);
    }
}
