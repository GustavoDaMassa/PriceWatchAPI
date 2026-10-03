namespace PriceWatch.Domain.Services;

/// <summary>
/// Monta a consulta usada para procurar o mesmo produto em outras lojas: palavras iniciais e
/// significativas do título, com a marca na frente e o modelo no fim quando o título não os traz.
/// Consultas longas demais fazem a busca por palavra-chave das lojas não achar nada.
/// </summary>
public static class SearchQueryBuilder
{
    private const int MaxTitleTokens = 6;
    private const int MaxTokens = 8;

    public static readonly HashSet<string> Stopwords = new()
    {
        "de", "da", "do", "das", "dos", "com", "para", "por", "em", "e", "a", "o", "as", "os",
        "um", "uma", "sem", "ou", "no", "na", "nos", "nas"
    };

    public static string Build(Domain.Entities.Product product)
    {
        var tokens = TitleSimilarity.Normalize(product.Name)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !Stopwords.Contains(t))
            .Distinct()
            .Take(MaxTitleTokens)
            .ToList();

        var brand = TitleSimilarity.Normalize(product.Brand ?? string.Empty);
        if (brand.Length > 0 && !tokens.Contains(brand))
            tokens.Insert(0, brand);

        var model = TitleSimilarity.Normalize(product.Model ?? string.Empty);
        if (model.Length > 0 && !Compact(string.Concat(tokens)).Contains(Compact(model)))
            tokens.AddRange(model.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return string.Join(' ', tokens.Take(MaxTokens));
    }

    private static string Compact(string value) => new(value.Where(char.IsLetterOrDigit).ToArray());
}
