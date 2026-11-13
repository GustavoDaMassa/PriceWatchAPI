using PriceWatch.Domain.Enums;
using PriceWatch.Domain.ValueObjects;

namespace PriceWatch.Domain.Interfaces.Services;

/// <summary>
/// Procura ofertas de um produto numa loja (descoberta em outras lojas, M3). Contrato: <b>nunca
/// lança</b>. Qualquer falha (credencial ausente, API fora do ar, resposta inesperada) vira lista
/// vazia, para uma loja com problema não derrubar a descoberta nas outras.
/// </summary>
public interface IOfferSearcher
{
    ProductSource ProductSource { get; }

    /// <summary>False quando a integração está desligada (ex.: credenciais ausentes).</summary>
    bool IsAvailable => true;

    /// <summary>Ofertas com exatamente este GTIN (alta confiança).</summary>
    Task<IReadOnlyList<OfferSearchResult>> SearchByGtinAsync(string gtin);

    /// <summary>Ofertas encontradas por palavra-chave (a pontuação e a confirmação do usuário decidem o resto).</summary>
    Task<IReadOnlyList<OfferSearchResult>> SearchByKeywordAsync(string query);

    /// <summary>
    /// Uma página de resultados da busca por palavra-chave (0 = a primeira, igual a <see cref="SearchByKeywordAsync"/>).
    /// Usada pela "nova busca" para ir além dos primeiros resultados. Lojas sem paginação devolvem vazio nas demais páginas.
    /// </summary>
    Task<IReadOnlyList<OfferSearchResult>> SearchByKeywordPageAsync(string query, int page) =>
        page == 0 ? SearchByKeywordAsync(query) : Task.FromResult<IReadOnlyList<OfferSearchResult>>([]);
}
