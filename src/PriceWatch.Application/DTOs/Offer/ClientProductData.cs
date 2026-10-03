namespace PriceWatch.Application.DTOs.Offer;

/// <summary>
/// Dados do produto lidos pelo cliente (extensão ou app) na página da loja. Só COMPLETAM marca e
/// modelo quando a fonte oficial da loja não trouxe. Preço e nome nunca vêm daqui (o cliente pode
/// ser adulterado e a fonte de verdade do preço é a API da loja).
///
/// GTIN e imagem do cliente são ignorados de propósito: o catálogo é compartilhado entre usuários e o
/// matching funde produtos pelo GTIN, então um GTIN informado por um cliente permitiria envenenar o
/// catálogo de todos. Eles voltam no M3 como sugestões que passam pela confirmação do usuário.
/// </summary>
public record ClientProductData(
    string? Brand = null,
    string? Model = null);
