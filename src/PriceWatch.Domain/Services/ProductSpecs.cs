using System.Globalization;
using System.Text.RegularExpressions;

namespace PriceWatch.Domain.Services;

/// <summary>
/// Extrai medidas (capacidade, tela, potência...) de um título. Variantes do mesmo modelo
/// ("128GB" x "256GB") têm títulos quase idênticos, então uma similaridade de texto sozinha as
/// funde por engano; comparar as medidas é o que separa uma da outra.
/// </summary>
public static partial class ProductSpecs
{
    public static IReadOnlyDictionary<string, HashSet<string>> Extract(string title)
    {
        var specs = new Dictionary<string, HashSet<string>>();

        foreach (Match match in MeasurementRegex().Matches(title ?? string.Empty))
        {
            var unit = NormalizeUnit(match.Groups["unit"].Value);
            var number = decimal.Parse(match.Groups["number"].Value.Replace(',', '.'), CultureInfo.InvariantCulture)
                .ToString("0.##", CultureInfo.InvariantCulture);

            if (!specs.TryGetValue(unit, out var values))
                specs[unit] = values = new HashSet<string>();
            values.Add(number);
        }

        return specs;
    }

    /// <summary>True quando uma unidade presente nos dois lados não tem nenhum valor em comum.</summary>
    public static bool Conflict(
        IReadOnlyDictionary<string, HashSet<string>> a,
        IReadOnlyDictionary<string, HashSet<string>> b) =>
        a.Any(pair => b.TryGetValue(pair.Key, out var other) && !pair.Value.Overlaps(other));

    private static string NormalizeUnit(string unit) => unit.ToLowerInvariant() switch
    {
        "\"" or "pol" or "polegada" or "polegadas" or "in" => "in",
        var u => u
    };

    // número + unidade; não casa dentro de códigos ("i5", "DDR5", "RTX4050") nem em palavras que
    // apenas começam com a letra da unidade ("16 galões")
    [GeneratedRegex(
        @"(?<![\p{L}\p{Nd}.,])(?<number>\d{1,5}(?:[.,]\d{1,2})?)\s?(?<unit>gb|tb|mb|mah|hz|ml|kg|mm|cm|polegadas?|pol|in|""|w|g|l)(?![\p{L}])",
        RegexOptions.IgnoreCase)]
    private static partial Regex MeasurementRegex();
}
