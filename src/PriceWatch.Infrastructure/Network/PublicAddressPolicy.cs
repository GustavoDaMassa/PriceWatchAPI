using System.Net;
using System.Net.Sockets;

namespace PriceWatch.Infrastructure.Network;

/// <summary>
/// Decide se um endereço é alcançável pela internet pública. Usado contra SSRF: o servidor nunca
/// deve seguir um link (ou um redirecionamento) para loopback, rede privada, link-local ou reservado.
/// </summary>
public static class PublicAddressPolicy
{
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicIPv4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsPublicIPv6(address),
            _ => false
        };
    }

    private static bool IsPublicIPv4(byte[] b) =>
        !(b[0] == 0 ||                                  // 0.0.0.0/8
          b[0] == 10 ||                                 // 10.0.0.0/8
          (b[0] == 100 && b[1] >= 64 && b[1] <= 127) || // 100.64.0.0/10 (CGNAT)
          b[0] == 127 ||                                // loopback
          (b[0] == 169 && b[1] == 254) ||               // link-local / metadados de nuvem
          (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||  // 172.16.0.0/12
          (b[0] == 192 && b[1] == 168) ||               // 192.168.0.0/16
          (b[0] == 198 && (b[1] == 18 || b[1] == 19)) ||// benchmark
          b[0] >= 224);                                 // multicast e reservado

    private static bool IsPublicIPv6(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.IPv6None) || address.Equals(IPAddress.IPv6Any))
            return false;
        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
            return false;

        var first = address.GetAddressBytes()[0];
        return (first & 0xFE) != 0xFC; // fc00::/7 (unique local)
    }
}
