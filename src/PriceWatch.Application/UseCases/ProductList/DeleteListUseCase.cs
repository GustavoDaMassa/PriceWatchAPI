using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.ProductList;

public class DeleteListUseCase
{
    private readonly IProductListRepository _repository;

    public DeleteListUseCase(IProductListRepository repository)
    {
        _repository = repository;
    }

    public async Task ExecuteAsync(string listId, string userId)
    {
        var list = await _repository.GetByIdAsync(listId);
        if (list is null || list.UserId != userId)
            throw new ProductListNotFoundException(listId);

        await _repository.DeleteAsync(listId);
    }
}
