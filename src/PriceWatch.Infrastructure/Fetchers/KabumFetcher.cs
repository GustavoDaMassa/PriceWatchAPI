using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Domain.ValueObjects;
using PriceWatch.Infrastructure.Affiliates;
using PriceWatch.Infrastructure.Settings;

namespace PriceWatch.Infrastructure.Fetchers;

/// <summary>
/// Busca preço/GTIN do Kabum via API de ofertas da Lomadee/SocialSoul (agregador de afiliados
/// escolhido na Fase 6 — ver docs/arquitetura.md). Diferente da API do Mercado Livre, a API da
/// Lomadee não faz lookup direto por URL — é busca por palavra-chave. Esta implementação:
/// 1. extrai uma palavra-chave do slug da URL do Kabum;
/// 2. busca ofertas por essa palavra-chave;
/// 3. entre os resultados, casa pela URL do produto (campo "link") mais próxima da URL pedida.
///
/// IMPORTANTE: o caminho exato do endpoint (`/offer/_search`) e os nomes de campo da resposta
/// (barcode/description/prices/images/link) seguem a documentação pública da Lomadee Developers
/// (developer.socialsoul.com.vc), mas não foram validados contra a API real — a Lomadee exige
/// cadastro de afiliado aprovado para gerar AppToken/SourceId. Precisa de um smoke test manual
/// assim que o dev tiver credenciais reais (mesma situação do AppToken vazio do Mercado Livre
/// em appsettings.json, preenchido só via variável de ambiente em produção).
/// </summary>
public partial class KabumFetcher : IPriceFetcher
{
    private readonly HttpClient _httpClient;
    private readonly LomadeeSettings _settings;

    public ProductSource ProductSource => ProductSource.Kabum;

    public bool IsAvailable => _settings.IsConfigured;

    public bool CanHandle(string url) =>
        _settings.IsConfigured && url.Contains("kabum.com.br", StringComparison.OrdinalIgnoreCase);

    public KabumFetcher(HttpClient httpClient, IOptions<LomadeeSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task<ProductFetchResult> FetchAsync(string url)
    {
        var keyword = ExtractKeywordFromSlug(url);
        if (string.IsNullOrWhiteSpace(keyword))
            throw new BusinessException("Invalid Kabum URL: could not extract a search keyword from the product slug.");

        var offers = (await LomadeeOffersApi.SearchAsync(_httpClient, _settings, keyword))
            .Where(o => o.Link is not null && o.Link.Contains("kabum.com.br", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var match = offers.FirstOrDefault(o => UrlsMatch(o.Link!, url)) ?? offers.FirstOrDefault();

        if (match is null)
            throw new BusinessException($"Could not find a matching Kabum offer for URL '{url}'.");

        var price = match.Prices?.Where(p => p.Price > 0).Min(p => (decimal?)p.Price) ?? 0;
        if (price <= 0)
            throw new BusinessException($"Could not read a valid price for Kabum offer matching '{url}'.");

        var imageUrl = match.Images?.FirstOrDefault();

        return new ProductFetchResult(price, match.Description ?? keyword, imageUrl, match.Barcode);
    }

    private static bool UrlsMatch(string candidateUrl, string requestedUrl) =>
        string.Equals(
            candidateUrl.TrimEnd('/').Split('?')[0],
            requestedUrl.TrimEnd('/').Split('?')[0],
            StringComparison.OrdinalIgnoreCase);

    // pega o último segmento não-numérico do path como palavra-chave de busca
    // (ex.: kabum.com.br/produto/123456/placa-de-video-rtx-4060 -> "placa de video rtx 4060")
    private static string ExtractKeywordFromSlug(string url)
    {
        var path = new Uri(url).AbsolutePath.Trim('/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var slug = segments.LastOrDefault(s => !NumericSegmentRegex().IsMatch(s));
        return slug is null ? string.Empty : slug.Replace('-', ' ');
    }

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex NumericSegmentRegex();
}
