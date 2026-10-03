using PriceWatch.Application.DTOs.Discovery;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Services;

namespace PriceWatch.Application.UseCases.Discovery;

/// <summary>As candidatas válidas de um produto que o usuário ainda não julgou.</summary>
public class GetOfferCandidatesUseCase
{
    private readonly IUserProductSubscriptionRepository _subscriptions;
    private readonly IOfferCandidateRepository _candidates;
    private readonly IOfferMatchFeedbackRepository _feedback;

    public GetOfferCandidatesUseCase(
        IUserProductSubscriptionRepository subscriptions,
        IOfferCandidateRepository candidates,
        IOfferMatchFeedbackRepository feedback)
    {
        _subscriptions = subscriptions;
        _candidates = candidates;
        _feedback = feedback;
    }

    public async Task<IReadOnlyList<OfferCandidateResponse>> ExecuteAsync(string userId, string productId)
    {
        if (await _subscriptions.GetByUserIdAndProductIdAsync(userId, productId) is null)
            throw new ProductNotFoundException(productId);

        var judged = (await _feedback.GetByUserAndProductAsync(userId, productId))
            .Select(f => f.CandidateId)
            .ToHashSet();

        var storeNames = StoreCatalog.All.ToDictionary(s => s.Source, s => s.Name);

        return (await _candidates.GetByProductIdAsync(productId))
            .Where(c => !judged.Contains(c.Id))
            .Select(c => new OfferCandidateResponse(
                c.Id, c.ProductId, c.Source,
                storeNames.GetValueOrDefault(c.Source, c.Source.ToString()),
                c.Url, c.Name, c.Price, c.ImageUrl, c.Gtin, c.Brand, c.Model, c.Score, c.Reason,
                Preselected: c.Reason == CandidateReason.Gtin))
            .ToList();
    }
}
