namespace PriceWatch.Application.Interfaces;

/// <summary>
/// Limita a descoberta de ofertas a uma execução por produto dentro de uma janela de tempo
/// (compartilhada entre usuários): evita repetir buscas nas APIs das lojas, que têm cota e custo,
/// e protege contra quem dispara a descoberta em loop.
/// </summary>
public interface IDiscoveryGate
{
    /// <returns>True se a descoberta pode rodar agora (e já reserva a janela); false se rodou há pouco.</returns>
    Task<bool> TryAcquireAsync(string productId, TimeSpan window);

    /// <summary>Quantas "novas buscas" seguidas do produto voltaram sem nada novo (zera quando uma acha algo, e expira sozinho).</summary>
    Task<int> CountEmptyRunsAsync(string productId);

    /// <summary>Registra o resultado de uma nova busca: sem nada novo soma 1 ao contador de vazias; com algo novo, zera.</summary>
    Task RecordNewSearchAsync(string productId, bool foundNew);
}
