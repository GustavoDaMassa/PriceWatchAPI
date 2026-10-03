namespace PriceWatch.Application.DTOs.Discovery;

public enum ConfirmOfferMatchOutcome
{
    /// <summary>"Não é o mesmo produto": registrado, a candidata some da lista do usuário.</summary>
    Rejected,

    /// <summary>"É o mesmo produto" sem acompanhar: registrado.</summary>
    Recorded,

    /// <summary>A oferta passou a ser acompanhada (aparece na comparação e no monitoramento).</summary>
    Attached,

    /// <summary>
    /// Voto registrado, mas o produto é acompanhado por outros usuários e a candidata não tem GTIN
    /// idêntico: a oferta só é anexada quando outro usuário confirmar também.
    /// </summary>
    Pending,

    /// <summary>A oferta já pertence a outro produto do catálogo e não é fundida por voto.</summary>
    Unavailable
}

public record ConfirmOfferMatchResponse(ConfirmOfferMatchOutcome Outcome, string ProductId, string? OfferId);
