using PriceWatch.Domain.Enums;

namespace PriceWatch.Domain.Entities;

/// <summary>
/// O veredito de um usuário sobre uma candidata ("é o mesmo produto?" e se quer acompanhar). É a
/// camada final de segurança do matching sem GTIN e, de quebra, um conjunto de dados rotulado para
/// medir (e um dia melhorar) a pontuação. Dado pessoal (userId): apagado com a conta.
/// </summary>
public class OfferMatchFeedback
{
    public string Id { get; private set; } = default!;
    public string UserId { get; private set; } = default!;
    public string CandidateId { get; private set; } = default!;
    public string ProductId { get; private set; } = default!;
    public string Url { get; private set; } = default!;
    public MatchVerdict Verdict { get; private set; }
    public bool Track { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private OfferMatchFeedback() { }

    public static OfferMatchFeedback Create(
        string userId, string candidateId, string productId, string url, MatchVerdict verdict, bool track) =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            CandidateId = candidateId,
            ProductId = productId,
            Url = url,
            Verdict = verdict,
            // quem diz "não é o mesmo produto" nunca passa a acompanhar a oferta
            Track = verdict == MatchVerdict.Same && track,
            CreatedAt = DateTime.UtcNow
        };

    public static OfferMatchFeedback Restore(
        string id, string userId, string candidateId, string productId, string url,
        MatchVerdict verdict, bool track, DateTime createdAt) =>
        new()
        {
            Id = id, UserId = userId, CandidateId = candidateId, ProductId = productId, Url = url,
            Verdict = verdict, Track = track, CreatedAt = createdAt
        };
}
