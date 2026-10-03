using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using PriceWatch.Application.Interfaces;
using PriceWatch.Domain.Exceptions;

namespace PriceWatch.Infrastructure.Network;

/// <summary>
/// Segue links curtos (ex.: o botão Compartilhar dos apps das lojas) até a URL final, com
/// defesa contra SSRF: só http/https nas portas 80/443, cada salto precisa resolver para um
/// endereço público, no máximo 5 redirecionamentos, 5 s por salto e o corpo nunca é lido.
/// Em produção o <c>HttpClient</c> também valida o IP no momento da conexão (<see cref="ConnectAsync"/>),
/// o que fecha a janela de DNS rebinding entre a checagem e a conexão.
/// </summary>
public class SafeUrlResolver : IUrlResolver
{
    private const int MaxRedirects = 5;
    private static readonly TimeSpan HopTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly IHostAddressResolver _hostResolver;
    private readonly ILogger<SafeUrlResolver> _logger;

    public SafeUrlResolver(HttpClient httpClient, IHostAddressResolver hostResolver, ILogger<SafeUrlResolver> logger)
    {
        _httpClient = httpClient;
        _hostResolver = hostResolver;
        _logger = logger;
    }

    public async Task<string> ResolveFinalUrlAsync(string url)
    {
        var current = ParseAllowed(url);

        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            await EnsureHostIsPublicAsync(current);

            using var timeout = new CancellationTokenSource(HopTimeout);
            HttpResponseMessage response;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // loja fora do ar ou lenta: devolve o que já se sabe; o import decide se a loja é suportada
                _logger.LogWarning(ex, "Could not follow {Url}; using it as the final URL.", current);
                return current.AbsoluteUri;
            }

            using (response)
            {
                if (!IsRedirect(response.StatusCode) || response.Headers.Location is null)
                    return current.AbsoluteUri;

                if (hop == MaxRedirects)
                    break;

                current = ParseAllowed(new Uri(current, response.Headers.Location).AbsoluteUri);
            }
        }

        throw new BusinessException("The link has too many redirects.");
    }

    // usado como ConnectCallback do SocketsHttpHandler: só conecta em endereços públicos
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        var publicAddresses = addresses.Where(PublicAddressPolicy.IsPublic).ToArray();
        if (publicAddresses.Length == 0)
            throw new HttpRequestException("Blocked: the host does not resolve to a public address.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(publicAddresses, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static Uri ParseAllowed(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new BusinessException("Only http and https links are supported.");

        if (uri.Port != 80 && uri.Port != 443)
            throw new BusinessException("Links to non-standard ports are not supported.");

        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new BusinessException("Links with embedded credentials are not supported.");

        return uri;
    }

    private async Task EnsureHostIsPublicAsync(Uri uri)
    {
        IPAddress[] addresses;
        try
        {
            addresses = IPAddress.TryParse(uri.DnsSafeHost, out var literal)
                ? [literal]
                : await _hostResolver.ResolveAsync(uri.DnsSafeHost, CancellationToken.None);
        }
        catch (SocketException)
        {
            throw new BusinessException("Could not resolve the link's host.");
        }

        if (addresses.Length == 0 || addresses.Any(a => !PublicAddressPolicy.IsPublic(a)))
            throw new BusinessException("The link points to a destination that is not allowed.");
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;
}
