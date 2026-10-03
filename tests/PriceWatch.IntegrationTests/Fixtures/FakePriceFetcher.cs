using System.Collections.Concurrent;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Domain.ValueObjects;

namespace PriceWatch.IntegrationTests.Fixtures;

/// <summary>
/// Fake compartilhado pela suíte inteira (mesma instância do factory, mesmo Mongo). Por padrão,
/// cada URL recebe um GTIN derivado dela mesma (determinístico, único por URL) — isso evita que
/// testes diferentes "colidam" sem querer no matching por GTIN (ver <c>MatchingFlowTests</c>).
/// Testes que precisam simular duas ofertas do MESMO produto físico usam <see cref="SetSharedGtin"/>.
/// </summary>
public class FakePriceFetcher : IPriceFetcher
{
    private readonly ConcurrentDictionary<string, string> _gtinOverridesByUrl = new();

    public ProductSource ProductSource => ProductSource.MercadoLivre;

    public bool CanHandle(string url) => true;

    public void SetSharedGtin(string url, string gtin) => _gtinOverridesByUrl[url] = gtin;

    public Task<ProductFetchResult> FetchAsync(string url)
    {
        var gtin = _gtinOverridesByUrl.TryGetValue(url, out var overriddenGtin)
            ? overriddenGtin
            : $"GTIN-{(uint)url.GetHashCode()}";

        return Task.FromResult(new ProductFetchResult(
            Price: 199.90m,
            Name: "Produto de Teste",
            ImageUrl: "https://example.com/image.png",
            Gtin: gtin,
            Brand: "MarcaTeste",
            Model: "ModeloTeste"));
    }
}
