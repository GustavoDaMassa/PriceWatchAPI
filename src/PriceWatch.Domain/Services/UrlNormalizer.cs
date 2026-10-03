using System.Text.RegularExpressions;
using PriceWatch.Domain.Exceptions;

namespace PriceWatch.Domain.Services;

/// <summary>
/// Normaliza a URL de um produto para o catálogo reconhecer o mesmo link colado de formas
/// diferentes (maiúsculas, porta padrão, fragmento, parâmetros de campanha). Só aceita http/https,
/// sem credenciais embutidas — o resto é recusado antes de qualquer acesso de rede.
/// </summary>
public static partial class UrlNormalizer
{
    private const int MaxLength = 2048;

    private static readonly HashSet<string> TrackingParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "gclid", "fbclid", "igshid", "msclkid", "dclid", "yclid", "mc_cid", "mc_eid", "_ga"
    };

    private static readonly string[] TrackingPrefixes = ["utm_", "matt_"];

    public static string Normalize(string? input)
    {
        var raw = input?.Trim();
        if (string.IsNullOrEmpty(raw))
            throw new BusinessException("URL is required.");
        if (raw.Length > MaxLength)
            throw new BusinessException("URL is too long.");

        if (!raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            // "javascript:...", "ftp://...", "file:///..." têm esquema e são recusados; "loja.com/x" não tem
            if (SchemePrefix().IsMatch(raw))
                throw new BusinessException("Only http and https URLs are supported.");
            raw = "https://" + raw;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(uri.Host))
            throw new BusinessException("Invalid URL.");

        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new BusinessException("URLs with embedded credentials are not supported.");

        var withoutQuery = uri.GetLeftPart(UriPartial.Path);
        var keptParameters = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !IsTracking(p))
            .ToList();

        return keptParameters.Count == 0 ? withoutQuery : $"{withoutQuery}?{string.Join('&', keptParameters)}";
    }

    private static bool IsTracking(string parameter)
    {
        var name = parameter.Split('=', 2)[0];
        return TrackingParameters.Contains(name) ||
               TrackingPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+.\-]*:")]
    private static partial Regex SchemePrefix();
}
