namespace PriceWatch.Domain.Services;

/// <summary>
/// Aceita só GTINs plausíveis (8, 12, 13 ou 14 dígitos: EAN-8, UPC-A, EAN-13, GTIN-14). Dados
/// vindos do cliente (extensão/app) passam por aqui antes de entrar no catálogo, porque o GTIN é a
/// chave do matching entre lojas e um valor errado juntaria produtos diferentes.
/// </summary>
public static class GtinNormalizer
{
    private static readonly int[] ValidLengths = [8, 12, 13, 14];

    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var cleaned = new string(input.Where(c => c is not (' ' or '-' or '\t')).ToArray());
        return cleaned.All(char.IsAsciiDigit) && ValidLengths.Contains(cleaned.Length) ? cleaned : null;
    }
}
