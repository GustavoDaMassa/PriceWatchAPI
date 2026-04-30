using PriceWatch.Application.DTOs.ProductList;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.ProductList;

public class GetListAnalysisUseCase
{
    private readonly IProductListRepository _listRepository;

    public GetListAnalysisUseCase(IProductListRepository listRepository)
    {
        _listRepository = listRepository;
    }

    // TODO: complete implementation in domain-tracked-product when ITrackedProductRepository is available
    public Task<IEnumerable<AnalysisItemDto>> ExecuteAsync(string listId, string userId)
    {
        return Task.FromResult(Enumerable.Empty<AnalysisItemDto>());
    }
}
