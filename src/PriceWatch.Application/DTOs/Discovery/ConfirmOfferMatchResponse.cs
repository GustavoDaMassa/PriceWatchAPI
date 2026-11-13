namespace PriceWatch.Application.DTOs.Discovery;

public enum ConfirmOfferMatchOutcome
{
    /// <summary>"Não é parecido": registrado, a candidata some da lista do usuário.</summary>
    Rejected,

    /// <summary>"É parecido" sem acompanhar: registrado.</summary>
    Recorded,

    /// <summary>A oferta virou um produto novo, na mesma lista do produto de origem, e passou a ser monitorada.</summary>
    Attached
}

/// <param name="ProductId">Em <c>Attached</c>, o produto NOVO criado; nos demais, o produto de origem.</param>
public record ConfirmOfferMatchResponse(ConfirmOfferMatchOutcome Outcome, string ProductId, string? OfferId);
