using PriceWatch.Domain.Entities;

namespace PriceWatch.Domain.Interfaces.Repositories;

public interface IOfferMatchFeedbackRepository
{
    Task<OfferMatchFeedback?> GetByUserAndCandidateAsync(string userId, string candidateId);
    Task<IEnumerable<OfferMatchFeedback>> GetByUserAndProductAsync(string userId, string productId);

    /// <summary>Quantos usuários distintos disseram "é o mesmo produto" e querem acompanhar a candidata.</summary>
    Task<int> CountTrackVotesAsync(string candidateId);

    /// <summary>Insere ou atualiza pelo id.</summary>
    Task SaveAsync(OfferMatchFeedback feedback);

    Task DeleteByUserIdAsync(string userId);
}
