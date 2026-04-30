using PriceWatch.Application.DTOs.ProductList;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.ProductList;

public class CreateListUseCase
{
    private readonly IProductListRepository _repository;

    public CreateListUseCase(IProductListRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProductListResponse> ExecuteAsync(string userId, CreateProductListRequest request)
    {
        var list = Domain.Entities.ProductList.Create(userId, request.Name, request.Description);
        await _repository.CreateAsync(list);
        return new ProductListResponse(list.Id, list.Name, list.Description, list.CreatedAt);
    }
}
