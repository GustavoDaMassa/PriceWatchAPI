namespace PriceWatch.Application.Interfaces;

/// <summary>Segue links curtos e redirecionamentos até a URL final de um produto.</summary>
public interface IUrlResolver
{
    /// <returns>A URL final. Lança <c>BusinessException</c> para destinos proibidos ou redirecionamento em excesso.</returns>
    Task<string> ResolveFinalUrlAsync(string url);
}
