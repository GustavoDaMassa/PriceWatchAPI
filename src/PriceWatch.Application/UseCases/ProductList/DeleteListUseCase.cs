using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.ProductList;

public class DeleteListUseCase
{
    private readonly IProductListRepository _listRepository;
    private readonly ITrackedProductRepository _productRepository;

    public DeleteListUseCase(IProductListRepository listRepository, ITrackedProductRepository productRepository)
    {
        _listRepository = listRepository;
        _productRepository = productRepository;
    }

    public async Task ExecuteAsync(string listId, string userId)
    {
        var list = await _listRepository.GetByIdAsync(listId);
        if (list is null || list.UserId != userId)
            throw new ProductListNotFoundException(listId);

        await _productRepository.UnlinkByListIdAsync(listId);
        await _listRepository.DeleteAsync(listId);
    }
}
