using PriceWatch.Domain.Enums;
using PriceWatch.Domain.ValueObjects;

namespace PriceWatch.Domain.Entities;

/// <summary>
/// Oferta encontrada em outra loja para um produto já acompanhado, aguardando a confirmação do
/// usuário ("é o mesmo produto?"). NÃO é uma <see cref="StoreOffer"/>: não é monitorada nem alerta
/// até ser confirmada (RI08). Compartilhada entre usuários (o catálogo é compartilhado) e expira sozinha.
/// </summary>
public class OfferCandidate
{
    public string Id { get; private set; } = default!;
    public string ProductId { get; private set; } = default!;
    public ProductSource Source { get; private set; }
    public string Url { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public decimal Price { get; private set; }
    public string? ImageUrl { get; private set; }
    public string? Gtin { get; private set; }
    public string? Brand { get; private set; }
    public string? Model { get; private set; }
    public double Score { get; private set; }
    public CandidateReason Reason { get; private set; }

    /// <summary>Veredito do juiz de IA ("é o mesmo produto?"); nulo quando a IA não foi consultada ou não respondeu.</summary>
    public bool? AiSaysSame { get; private set; }

    /// <summary>Justificativa curta da IA, mostrada ao usuário.</summary>
    public string? AiReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private OfferCandidate() { }

    public static OfferCandidate Create(
        string productId,
        ProductSource source,
        OfferSearchResult found,
        double score,
        CandidateReason reason,
        TimeSpan timeToLive)
    {
        var now = DateTime.UtcNow;
        return new OfferCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ProductId = productId,
            Source = source,
            Url = found.Url,
            Name = found.Name,
            Price = found.Price,
            ImageUrl = found.ImageUrl,
            Gtin = found.Gtin,
            Brand = found.Brand,
            Model = found.Model,
            Score = score,
            Reason = reason,
            CreatedAt = now,
            ExpiresAt = now.Add(timeToLive)
        };
    }

    public static OfferCandidate Restore(
        string id, string productId, ProductSource source, string url, string name, decimal price,
        string? imageUrl, string? gtin, string? brand, string? model, double score, CandidateReason reason,
        DateTime createdAt, DateTime expiresAt, bool? aiSaysSame = null, string? aiReason = null) =>
        new()
        {
            Id = id, ProductId = productId, Source = source, Url = url, Name = name, Price = price,
            ImageUrl = imageUrl, Gtin = gtin, Brand = brand, Model = model, Score = score, Reason = reason,
            CreatedAt = createdAt, ExpiresAt = expiresAt, AiSaysSame = aiSaysSame, AiReason = aiReason
        };

    public void ApplyJudgement(bool sameProduct, string reason)
    {
        AiSaysSame = sameProduct;
        AiReason = reason;
    }

    // nova busca encontrou a mesma oferta (mesma loja e URL): atualiza preço/nome e renova a validade,
    // mantendo o id para os vereditos já dados continuarem apontando para ela
    public void Refresh(OfferSearchResult found, double score, CandidateReason reason, TimeSpan timeToLive)
    {
        Name = found.Name;
        Price = found.Price;
        ImageUrl = found.ImageUrl;
        Gtin = found.Gtin;
        Brand = found.Brand;
        Model = found.Model;
        Score = score;
        Reason = reason;
        ExpiresAt = DateTime.UtcNow.Add(timeToLive);
    }

    public bool IsExpired() => ExpiresAt <= DateTime.UtcNow;
}
