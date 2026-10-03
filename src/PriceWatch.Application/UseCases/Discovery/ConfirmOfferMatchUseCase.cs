using PriceWatch.Application.DTOs.Discovery;
using PriceWatch.Application.Interfaces;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.Discovery;

/// <summary>
/// O usuário responde "é o mesmo produto?" e, se quiser, passa a acompanhar a oferta. O veredito é
/// sempre gravado (é o dado rotulado do matching). Anexar uma oferta a um produto altera o catálogo
/// COMPARTILHADO (comparação e alertas de todos os assinantes), então um voto isolado não basta:
/// anexa na hora só se (a) a candidata veio por GTIN idêntico (evidência objetiva), ou (b) quem votou é
/// o único assinante do produto (ninguém mais é afetado), ou (c) um segundo usuário confirmou também.
/// Produtos do catálogo nunca são fundidos por voto.
/// </summary>
public class ConfirmOfferMatchUseCase
{
    private const int QuorumVotes = 2;

    private readonly IOfferCandidateRepository _candidates;
    private readonly IOfferMatchFeedbackRepository _feedback;
    private readonly IUserProductSubscriptionRepository _subscriptions;
    private readonly IProductRepository _products;
    private readonly IStoreOfferRepository _offers;
    private readonly IAffiliateLinkResolver _affiliateResolver;
    private readonly SyncOfferActivityUseCase _syncActivity;

    public ConfirmOfferMatchUseCase(
        IOfferCandidateRepository candidates,
        IOfferMatchFeedbackRepository feedback,
        IUserProductSubscriptionRepository subscriptions,
        IProductRepository products,
        IStoreOfferRepository offers,
        IAffiliateLinkResolver affiliateResolver,
        SyncOfferActivityUseCase syncActivity)
    {
        _candidates = candidates;
        _feedback = feedback;
        _subscriptions = subscriptions;
        _products = products;
        _offers = offers;
        _affiliateResolver = affiliateResolver;
        _syncActivity = syncActivity;
    }

    public async Task<ConfirmOfferMatchResponse> ExecuteAsync(string userId, string candidateId, ConfirmOfferMatchRequest request)
    {
        var candidate = await _candidates.GetByIdAsync(candidateId);
        if (candidate is null || candidate.IsExpired())
            throw new OfferCandidateNotFoundException(candidateId);

        // quem não acompanha o produto recebe o mesmo 404 de uma candidata inexistente
        var subscription = await _subscriptions.GetByUserIdAndProductIdAsync(userId, candidate.ProductId)
            ?? throw new OfferCandidateNotFoundException(candidateId);

        var verdict = request.IsSameProduct ? MatchVerdict.Same : MatchVerdict.NotSame;
        await SaveFeedbackAsync(userId, candidate, verdict, request.Track);

        if (verdict == MatchVerdict.NotSame)
            return new ConfirmOfferMatchResponse(ConfirmOfferMatchOutcome.Rejected, candidate.ProductId, null);

        if (!request.Track)
            return new ConfirmOfferMatchResponse(ConfirmOfferMatchOutcome.Recorded, candidate.ProductId, null);

        if (request.TargetPrice is > 0m)
        {
            subscription.SetTargetPrice(request.TargetPrice.Value);
            await _subscriptions.UpdateAsync(subscription);
        }

        if (!await IsSafeToAttachAsync(userId, candidate))
            return new ConfirmOfferMatchResponse(ConfirmOfferMatchOutcome.Pending, candidate.ProductId, null);

        var offer = await AttachAsync(candidate);
        return offer is null
            ? new ConfirmOfferMatchResponse(ConfirmOfferMatchOutcome.Unavailable, candidate.ProductId, null)
            : new ConfirmOfferMatchResponse(ConfirmOfferMatchOutcome.Attached, candidate.ProductId, offer.Id);
    }

    private async Task SaveFeedbackAsync(string userId, OfferCandidate candidate, MatchVerdict verdict, bool track)
    {
        var feedback = await _feedback.GetByUserAndCandidateAsync(userId, candidate.Id);
        if (feedback is null)
            feedback = OfferMatchFeedback.Create(userId, candidate.Id, candidate.ProductId, candidate.Url, verdict, track);
        else
            feedback.Revise(verdict, track);

        await _feedback.SaveAsync(feedback);
    }

    private async Task<bool> IsSafeToAttachAsync(string userId, OfferCandidate candidate)
    {
        if (candidate.Reason == CandidateReason.Gtin)
            return true;

        var otherActiveSubscribers = (await _subscriptions.GetByProductIdAsync(candidate.ProductId))
            .Any(s => s.IsActive && s.UserId != userId);
        if (!otherActiveSubscribers)
            return true;

        return await _feedback.CountTrackVotesAsync(candidate.Id) >= QuorumVotes;
    }

    private async Task<StoreOffer?> AttachAsync(OfferCandidate candidate)
    {
        var offer = await _offers.GetBySourceAndUrlAsync(candidate.Source, candidate.Url);

        if (offer is not null && offer.ProductId != candidate.ProductId)
            return null; // já pertence a outro produto do catálogo: não se funde por voto

        if (offer is null)
        {
            offer = StoreOffer.Create(candidate.ProductId, candidate.Source, candidate.Url, candidate.Gtin);
            offer.RecordPrice(candidate.Price);
            offer.PostponeCheck(TimeSpan.Zero); // o worker confirma o preço real logo na próxima rodada
            await OfferAffiliateLinker.TrySetAsync(_affiliateResolver, offer);
            await _offers.CreateAsync(offer);
        }

        var product = await _products.GetByIdAsync(candidate.ProductId);
        if (product is not null && product.MatchStatus != MatchStatus.Confirmed)
        {
            product.Confirm();
            await _products.UpdateAsync(product);
        }

        await _syncActivity.ExecuteAsync(candidate.ProductId);
        return offer;
    }
}
