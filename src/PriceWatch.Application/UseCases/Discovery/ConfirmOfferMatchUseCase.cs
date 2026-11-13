using PriceWatch.Application.DTOs.Discovery;
using PriceWatch.Application.DTOs.Offer;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.Discovery;

/// <summary>
/// O usuário responde "é parecido o bastante para acompanhar?" e, se quiser, passa a acompanhar a oferta.
/// O veredito é sempre gravado (é o dado rotulado do matching). Quem decide é só o próprio usuário: a
/// oferta aceita vira um produto NOVO na mesma lista do produto de origem, com histórico, preço-alvo e
/// alertas próprios, então nunca altera o catálogo de outros assinantes e não precisa de segunda confirmação.
/// </summary>
public class ConfirmOfferMatchUseCase
{
    private readonly IOfferCandidateRepository _candidates;
    private readonly IOfferMatchFeedbackRepository _feedback;
    private readonly IUserProductSubscriptionRepository _subscriptions;
    private readonly ICreateOfferUseCase _createOffer;

    public ConfirmOfferMatchUseCase(
        IOfferCandidateRepository candidates,
        IOfferMatchFeedbackRepository feedback,
        IUserProductSubscriptionRepository subscriptions,
        ICreateOfferUseCase createOffer)
    {
        _candidates = candidates;
        _feedback = feedback;
        _subscriptions = subscriptions;
        _createOffer = createOffer;
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

        var targetPrice = request.TargetPrice is > 0m ? request.TargetPrice.Value : subscription.TargetPrice;
        var created = await _createOffer.ExecuteAsync(userId, new CreateOfferRequest(candidate.Url, targetPrice, subscription.ListId));

        return new ConfirmOfferMatchResponse(ConfirmOfferMatchOutcome.Attached, created.ProductId, created.OfferId);
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
}
