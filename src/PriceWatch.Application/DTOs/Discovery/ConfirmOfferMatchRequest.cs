namespace PriceWatch.Application.DTOs.Discovery;

/// <param name="IsSameProduct">"É o mesmo produto?"</param>
/// <param name="Track">Quer acompanhar esta oferta (só vale se for o mesmo produto).</param>
/// <param name="TargetPrice">Preço-alvo opcional para o produto acompanhado.</param>
public record ConfirmOfferMatchRequest(bool IsSameProduct, bool Track, decimal? TargetPrice = null);
