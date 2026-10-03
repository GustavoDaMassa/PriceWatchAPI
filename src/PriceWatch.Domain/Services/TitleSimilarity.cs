using System.Text.RegularExpressions;

namespace PriceWatch.Domain.Services;

/// <summary>
/// Heurística de fallback quando não há GTIN/EAN disponível (RF15). Não é o algoritmo final —
/// é o ponto de extensão v1: normalização de título + similaridade por distância de edição.
/// Trocar por embeddings/classificador é um upgrade isolado a este arquivo (ver docs/arquitetura.md).
/// </summary>
public static partial class TitleSimilarity
{
    public static string Normalize(string title)
    {
        var lower = title.Trim().ToLowerInvariant();
        var withoutPunctuation = PunctuationRegex().Replace(lower, " ");
        var collapsed = WhitespaceRegex().Replace(withoutPunctuation, " ").Trim();
        return collapsed;
    }

    /// <summary>Similaridade entre 0 (nada em comum) e 1 (idênticos), baseada em distância de Levenshtein.</summary>
    public static double Ratio(string a, string b)
    {
        var normalizedA = Normalize(a);
        var normalizedB = Normalize(b);

        if (normalizedA.Length == 0 && normalizedB.Length == 0)
            return 1d;

        var distance = LevenshteinDistance(normalizedA, normalizedB);
        var maxLength = Math.Max(normalizedA.Length, normalizedB.Length);

        return maxLength == 0 ? 1d : 1d - (double)distance / maxLength;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var lengthA = a.Length;
        var lengthB = b.Length;
        var distances = new int[lengthA + 1, lengthB + 1];

        for (var i = 0; i <= lengthA; i++) distances[i, 0] = i;
        for (var j = 0; j <= lengthB; j++) distances[0, j] = j;

        for (var i = 1; i <= lengthA; i++)
        {
            for (var j = 1; j <= lengthB; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + cost);
            }
        }

        return distances[lengthA, lengthB];
    }

    [GeneratedRegex(@"[^\p{L}\p{Nd}\s]")]
    private static partial Regex PunctuationRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
