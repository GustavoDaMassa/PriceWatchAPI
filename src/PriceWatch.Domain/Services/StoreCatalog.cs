using PriceWatch.Domain.Enums;

namespace PriceWatch.Domain.Services;

public record StoreInfo(ProductSource Source, string Name, string HomeUrl);

/// <summary>
/// Lojas conhecidas pelo produto (nome e página inicial para o seletor de lojas). Se a loja está
/// de fato disponível (credenciais, API) quem diz são as capacidades dos fetchers e geradores.
/// <c>ProductSource.Manual</c> não é uma loja. Novas lojas entram sempre no final do enum (RNF14).
/// </summary>
public static class StoreCatalog
{
    public static IReadOnlyList<StoreInfo> All { get; } =
    [
        new(ProductSource.MercadoLivre, "Mercado Livre", "https://www.mercadolivre.com.br"),
        new(ProductSource.Kabum, "KaBuM!", "https://www.kabum.com.br"),
    ];
}
