using PriceWatch.Application.DTOs.TrackedProduct;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.TrackedProduct;

public class GetPriceHistoryUseCase
{
    private readonly IPriceSnapshotRepository _snapshotRepository;
    private readonly ITrackedProductRepository _productRepository;

    public GetPriceHistoryUseCase(
        IPriceSnapshotRepository snapshotRepository,
        ITrackedProductRepository productRepository)
    {
        _snapshotRepository = snapshotRepository;
        _productRepository = productRepository;
    }

    public async Task<IEnumerable<PriceSnapshotResponse>> ExecuteAsync(string productId, string userId)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        if (product is null || product.UserId != userId)
            throw new TrackedProductNotFoundException(productId);

        var snapshots = await _snapshotRepository.GetByProductIdAsync(productId);
        return snapshots.Select(s => new PriceSnapshotResponse(s.Id, s.Price, s.CapturedAt));
    }
}
