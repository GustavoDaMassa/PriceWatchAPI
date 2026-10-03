using PriceWatch.Domain.Enums;

namespace PriceWatch.Application.DTOs.Store;

/// <summary>
/// O que a plataforma consegue fazer com uma loja hoje. Os clientes adaptam o fluxo a isto: loja
/// sem <c>CanFetchByUrl</c> não aparece no seletor, loja sem <c>CanSearch</c> não entra na
/// descoberta de ofertas, loja sem <c>CanGenerateAffiliateLink</c> redireciona sem comissão.
/// </summary>
public record StoreCapabilitiesResponse(
    ProductSource Source,
    string Name,
    string HomeUrl,
    bool CanFetchByUrl,
    bool CanSearch,
    bool CanGenerateAffiliateLink);
