using PriceWatch.Application.DTOs.ProductList;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.ProductList;

public class GetUserListsUseCase
{
    private readonly IProductListRepository _repository;

    public GetUserListsUseCase(IProductListRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<ProductListResponse>> ExecuteAsync(string userId)
    {
        var lists = await _repository.GetByUserIdAsync(userId);
        return lists.Select(l => new ProductListResponse(l.Id, l.Name, l.Description, l.CreatedAt));
    }
}
