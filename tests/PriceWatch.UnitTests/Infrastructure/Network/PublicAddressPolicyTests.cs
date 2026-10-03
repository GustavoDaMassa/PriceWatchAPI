using System.Net;
using FluentAssertions;
using PriceWatch.Infrastructure.Network;
using Xunit;

namespace PriceWatch.UnitTests.Infrastructure.Network;

public class PublicAddressPolicyTests
{
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("142.250.78.14")]
    [InlineData("2606:4700:4700::1111")]
    public void IsPublic_AcceptsRoutableInternetAddresses(string ip)
    {
        PublicAddressPolicy.IsPublic(IPAddress.Parse(ip)).Should().BeTrue();
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.1.2.3")]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.0.244")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:10.0.0.1")]
    public void IsPublic_RejectsLoopbackPrivateLinkLocalAndReservedAddresses(string ip)
    {
        PublicAddressPolicy.IsPublic(IPAddress.Parse(ip)).Should().BeFalse();
    }

    [Theory]
    [InlineData("172.15.0.1", true)]
    [InlineData("172.32.0.1", true)]
    [InlineData("100.63.0.1", true)]
    [InlineData("100.128.0.1", true)]
    public void IsPublic_DoesNotOverBlockAddressesJustOutsideThePrivateRanges(string ip, bool expected)
    {
        PublicAddressPolicy.IsPublic(IPAddress.Parse(ip)).Should().Be(expected);
    }
}
