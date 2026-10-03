using PriceWatch.Application.DTOs.Offer;
using PriceWatch.Application.Interfaces;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Services;

namespace PriceWatch.Application.UseCases.Offers;

/// <summary>
/// Porta de entrada do fluxo "colar o link": normaliza a URL (descarta parâmetros de campanha),
/// segue links curtos de compartilhamento até a URL final da loja (só se a URL colada não for de
/// uma loja suportada, para evitar acesso de rede desnecessário) e delega a criação da oferta.
/// </summary>
public class ImportFromLinkUseCase
{
    private readonly IPriceFetcherResolver _fetchers;
    private readonly IUrlResolver _urlResolver;
    private readonly ICreateOfferUseCase _createOffer;

    public ImportFromLinkUseCase(
        IPriceFetcherResolver fetchers,
        IUrlResolver urlResolver,
        ICreateOfferUseCase createOffer)
    {
        _fetchers = fetchers;
        _urlResolver = urlResolver;
        _createOffer = createOffer;
    }

    public async Task<OfferResponse> ExecuteAsync(string userId, CreateOfferRequest request)
    {
        var url = UrlNormalizer.Normalize(request.Url);

        if (!_fetchers.CanHandle(url))
        {
            var finalUrl = UrlNormalizer.Normalize(await _urlResolver.ResolveFinalUrlAsync(url));
            if (!_fetchers.CanHandle(finalUrl))
                throw new BusinessException("This store is not supported yet.");

            url = finalUrl;
        }

        return await _createOffer.ExecuteAsync(userId, request with { Url = url });
    }
}
