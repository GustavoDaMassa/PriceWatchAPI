using PriceWatch.Application.DTOs.TrackedProduct;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.TrackedProduct;

public class AssignProductToListUseCase
{
    private readonly ITrackedProductRepository _productRepository;
    private readonly IProductListRepository _listRepository;

    public AssignProductToListUseCase(
        ITrackedProductRepository productRepository,
        IProductListRepository listRepository)
    {
        _productRepository = productRepository;
        _listRepository = listRepository;
    }

    public async Task ExecuteAsync(string productId, string userId, AssignToListRequest request)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        if (product is null || product.UserId != userId)
            throw new TrackedProductNotFoundException(productId);

        if (request.ListId is not null)
        {
            var list = await _listRepository.GetByIdAsync(request.ListId);
            if (list is null || list.UserId != userId)
                throw new ProductListNotFoundException(request.ListId);

            product.AssignToList(request.ListId);
        }
        else
            product.RemoveFromList();

        await _productRepository.UpdateAsync(product);
    }
}
