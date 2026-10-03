using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.ValueObjects;

namespace PriceWatch.Domain.Services;

/// <summary>
/// Decide se uma oferta encontrada em outra loja parece ser o mesmo produto, em camadas
/// deterministas (sem IA): GTIN, marca, medidas, modelo e similaridade de título. Prefere perder
/// candidatas a sugerir produtos diferentes: mesmo aceita, quem decide é o usuário na confirmação.
/// </summary>
public static class CandidateScorer
{
    private const double ThresholdWithModelMatch = 0.35;
    private const double ThresholdWithBrandMatch = 0.6;
    private const double ThresholdTitleOnly = 0.8;
    private const double ThresholdWhenTheModelIsMissingFromTheCandidate = 0.9;

    public static CandidateScore Score(Product source, OfferSearchResult candidate)
    {
        var sourceGtin = NormalizeGtin(source.Gtin);
        var candidateGtin = NormalizeGtin(candidate.Gtin);
        if (sourceGtin is not null && candidateGtin is not null)
        {
            return sourceGtin == candidateGtin
                ? new CandidateScore(true, 1d, CandidateReason.Gtin)
                : Rejected;
        }

        var sourceName = TitleSimilarity.Normalize(source.Name);
        var candidateName = TitleSimilarity.Normalize(candidate.Name);

        var brand = CompareBrand(source.Brand, candidate.Brand, candidateName);
        if (brand == BrandMatch.Conflict)
            return Rejected;

        if (ProductSpecs.Conflict(ProductSpecs.Extract(source.Name), ProductSpecs.Extract(candidate.Name)))
            return Rejected;

        var model = CompareModel(source.Model, candidate.Model, candidateName);
        if (model == ModelMatch.Conflict)
            return Rejected;

        var combined = 0.5 * TitleSimilarity.Ratio(sourceName, candidateName) + 0.5 * TokenOverlap(sourceName, candidateName);

        var threshold = model switch
        {
            ModelMatch.Match => ThresholdWithModelMatch,
            ModelMatch.MissingInCandidate => ThresholdWhenTheModelIsMissingFromTheCandidate,
            _ => brand == BrandMatch.Match ? ThresholdWithBrandMatch : ThresholdTitleOnly
        };

        if (combined < threshold)
            return Rejected;

        var score = combined + (brand == BrandMatch.Match ? 0.1 : 0) + (model == ModelMatch.Match ? 0.1 : 0);
        return new CandidateScore(true, Math.Min(score, 0.99), CandidateReason.Keyword);
    }

    private static readonly CandidateScore Rejected = new(false, 0d, CandidateReason.Keyword);

    private enum BrandMatch { Unknown, Match, Conflict }
    private enum ModelMatch { Unknown, Match, MissingInCandidate, Conflict }

    private static BrandMatch CompareBrand(string? sourceBrand, string? candidateBrand, string candidateName)
    {
        var source = TitleSimilarity.Normalize(sourceBrand ?? string.Empty);
        if (source.Length == 0)
            return BrandMatch.Unknown;

        var candidate = TitleSimilarity.Normalize(candidateBrand ?? string.Empty);
        if (candidate.Length > 0)
            return candidate.Contains(source) || source.Contains(candidate) ? BrandMatch.Match : BrandMatch.Conflict;

        // a loja não informa marca: procura no título
        return candidateName.Split(' ').Contains(source) ? BrandMatch.Match : BrandMatch.Unknown;
    }

    private static ModelMatch CompareModel(string? sourceModel, string? candidateModel, string candidateName)
    {
        var source = Compact(sourceModel);
        if (source.Length < 3)
            return ModelMatch.Unknown;

        var candidate = Compact(candidateModel);
        if (candidate.Length >= 3)
            return candidate.Contains(source) || source.Contains(candidate) ? ModelMatch.Match : ModelMatch.Conflict;

        return Compact(candidateName).Contains(source) ? ModelMatch.Match : ModelMatch.MissingInCandidate;
    }

    // overlap coefficient: robusto a títulos com palavras extras ou em outra ordem
    private static double TokenOverlap(string a, string b)
    {
        var tokensA = Tokens(a);
        var tokensB = Tokens(b);
        if (tokensA.Count == 0 || tokensB.Count == 0)
            return 0d;

        return (double)tokensA.Intersect(tokensB).Count() / Math.Min(tokensA.Count, tokensB.Count);
    }

    private static HashSet<string> Tokens(string normalizedName) =>
        normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2 && !SearchQueryBuilder.Stopwords.Contains(t))
            .ToHashSet();

    private static string Compact(string? value) =>
        new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string? NormalizeGtin(string? gtin) => GtinNormalizer.Normalize(gtin)?.TrimStart('0');
}
