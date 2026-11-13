using PriceWatch.Domain.Entities;
using PriceWatch.Domain.ValueObjects;

namespace PriceWatch.Domain.Interfaces.Services;

/// <summary>
/// Segunda opinião por IA quando o GTIN não resolve: diz se cada candidata é o mesmo produto do acompanhado
/// e sugere uma busca melhor que a montada por regras. Contrato: <b>nunca lança</b>. Qualquer falha (sem
/// chave, limite do plano grátis, resposta inválida) devolve <c>null</c> e a descoberta segue só com as regras.
/// </summary>
public interface IMatchJudge
{
    /// <summary>False quando a integração está desligada (ex.: chave ausente).</summary>
    bool IsAvailable { get; }

    /// <summary>Um veredito por candidata (pela posição na lista), ou <c>null</c> se a IA não respondeu.</summary>
    Task<IReadOnlyList<MatchJudgement>?> JudgeAsync(Product source, IReadOnlyList<OfferSearchResult> candidates);

    /// <summary>Palavras de busca para achar o mesmo produto em uma loja, ou <c>null</c> se a IA não respondeu.</summary>
    Task<string?> SuggestSearchQueryAsync(Product source);
}
