using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;

namespace PriceWatch.API.Extensions;

public static class ProxyHeadersExtensions
{
    /// <summary>
    /// Atrás do nginx (e do Cloudflare) o IP do cliente chega em X-Forwarded-For; sem isso o rate limit por IP
    /// enxergaria só o IP do nginx e todos os visitantes dividiriam o mesmo balde. Confia em qualquer proxy
    /// porque a API não é publicada: só o nginx da rede interna do Docker a alcança.
    /// </summary>
    public static IServiceCollection AddProxyHeaders(this IServiceCollection services) =>
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
            options.ForwardLimit = 1;
        });
}
