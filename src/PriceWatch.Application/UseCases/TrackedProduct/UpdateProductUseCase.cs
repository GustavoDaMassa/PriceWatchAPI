using PriceWatch.Application.DTOs.TrackedProduct;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.TrackedProduct;

public class UpdateProductUseCase
{
    private readonly ITrackedProductRepository _repository;

    public UpdateProductUseCase(ITrackedProductRepository repository)
    {
        _repository = repository;
    }

    public async Task ExecuteAsync(string productId, string userId, UpdateProductRequest request)
    {
        var product = await _repository.GetByIdAsync(productId);
        if (product is null || product.UserId != userId)
            throw new TrackedProductNotFoundException(productId);

        if (!request.IsActive)
            product.Deactivate();

        await _repository.UpdateAsync(product);
    }
}
