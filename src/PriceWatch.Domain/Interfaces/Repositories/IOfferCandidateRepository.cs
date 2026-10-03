using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;

namespace PriceWatch.Domain.Interfaces.Repositories;

public interface IOfferCandidateRepository
{
    Task<OfferCandidate?> GetByIdAsync(string id);

    /// <summary>Candidatas ainda válidas (não expiradas) do produto, da mais para a menos provável.</summary>
    Task<IEnumerable<OfferCandidate>> GetByProductIdAsync(string productId);

    Task<OfferCandidate?> GetByKeyAsync(string productId, ProductSource source, string url);

    /// <summary>Insere ou atualiza pelo id.</summary>
    Task SaveAsync(OfferCandidate candidate);
}
