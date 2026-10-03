namespace PriceWatch.Domain.Entities;

/// <summary>
/// Um clique de usuário numa oferta (saída para a loja pelo link de afiliado). É a base das
/// métricas de conversão da receita de afiliado e dado pessoal (userId): é apagado junto com a conta.
/// </summary>
public class ClickLog
{
    public string Id { get; private set; } = default!;
    public string OfferId { get; private set; } = default!;
    public string UserId { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }

    private ClickLog() { }

    public static ClickLog Create(string offerId, string userId) => new()
    {
        Id = Guid.NewGuid().ToString(),
        OfferId = offerId,
        UserId = userId,
        CreatedAt = DateTime.UtcNow
    };

    public static ClickLog Restore(string id, string offerId, string userId, DateTime createdAt) => new()
    {
        Id = id,
        OfferId = offerId,
        UserId = userId,
        CreatedAt = createdAt
    };
}
