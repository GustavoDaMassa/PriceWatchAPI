using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.ProductList;

public class DeleteListUseCase
{
    private readonly IProductListRepository _listRepository;
    private readonly ITrackedProductRepository _productRepository;
    private readonly IUserProductSubscriptionRepository _subscriptionRepository;

    public DeleteListUseCase(
        IProductListRepository listRepository,
        ITrackedProductRepository productRepository,
        IUserProductSubscriptionRepository subscriptionRepository)
    {
        _listRepository = listRepository;
        _productRepository = productRepository;
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task ExecuteAsync(string listId, string userId)
    {
        var list = await _listRepository.GetByIdAsync(listId);
        if (list is null || list.UserId != userId)
            throw new ProductListNotFoundException(listId);

        // os dois modelos: o item pode estar no TrackedProduct legado e/ou no catálogo unificado
        await _productRepository.UnlinkByListIdAsync(listId);
        await _subscriptionRepository.UnlinkByListIdAsync(listId);
        await _listRepository.DeleteAsync(listId);
    }
}
