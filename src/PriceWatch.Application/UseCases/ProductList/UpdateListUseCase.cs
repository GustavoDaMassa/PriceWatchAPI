using PriceWatch.Application.DTOs.ProductList;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.ProductList;

public class UpdateListUseCase
{
    private readonly IProductListRepository _repository;

    public UpdateListUseCase(IProductListRepository repository)
    {
        _repository = repository;
    }

    public async Task ExecuteAsync(string listId, string userId, UpdateProductListRequest request)
    {
        var list = await _repository.GetByIdAsync(listId);
        if (list is null || list.UserId != userId)
            throw new ProductListNotFoundException(listId);

        list.Update(request.Name, request.Description);
        await _repository.UpdateAsync(list);
    }
}
