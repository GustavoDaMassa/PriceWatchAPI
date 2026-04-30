using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.TrackedProduct;

public class RemoveProductUseCase
{
    private readonly ITrackedProductRepository _repository;

    public RemoveProductUseCase(ITrackedProductRepository repository)
    {
        _repository = repository;
    }

    public async Task ExecuteAsync(string productId, string userId)
    {
        var product = await _repository.GetByIdAsync(productId);
        if (product is null || product.UserId != userId)
            throw new TrackedProductNotFoundException(productId);

        await _repository.DeleteAsync(productId);
    }
}
