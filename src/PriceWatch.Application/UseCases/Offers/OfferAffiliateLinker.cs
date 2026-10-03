using PriceWatch.Application.Interfaces;
using PriceWatch.Domain.Entities;

namespace PriceWatch.Application.UseCases.Offers;

/// <summary>
/// Gera e guarda o link de afiliado de uma oferta ao começar o acompanhamento. Só salva se uma rede
/// está disponível E devolveu um link diferente da URL original (as redes devolvem a própria URL
/// quando falham; cachear isso esconderia a falha para sempre). Sem link, o redirect tenta de novo.
/// </summary>
internal static class OfferAffiliateLinker
{
    public static async Task<bool> TrySetAsync(IAffiliateLinkResolver resolver, StoreOffer offer)
    {
        if (!string.IsNullOrEmpty(offer.AffiliateUrl))
            return false;

        var generator = resolver.Resolve(offer.Source);
        if (!generator.IsAvailable)
            return false;

        var affiliateUrl = await generator.GenerateAsync(offer.Url);
        if (string.IsNullOrWhiteSpace(affiliateUrl) || string.Equals(affiliateUrl, offer.Url, StringComparison.Ordinal))
            return false;

        offer.SetAffiliateUrl(affiliateUrl);
        return true;
    }
}
