using FluentAssertions;
using Moq;
using PriceWatch.Application.DTOs.Devices;
using PriceWatch.Application.UseCases.Devices;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using Xunit;

namespace PriceWatch.UnitTests.Application.UseCases.Devices;

public class DeviceUseCasesTests
{
    private readonly Mock<IDeviceTokenRepository> _devices = new();
    private readonly RegisterDeviceUseCase _register;
    private readonly UnregisterDeviceUseCase _unregister;
    private const string Token = "fcm-token-with-enough-characters-1234567890";

    public DeviceUseCasesTests()
    {
        _devices.Setup(r => r.GetByTokenAsync(It.IsAny<string>())).ReturnsAsync((DeviceToken?)null);
        _register = new RegisterDeviceUseCase(_devices.Object);
        _unregister = new UnregisterDeviceUseCase(_devices.Object);
    }

    [Fact]
    public async Task Register_WithANewToken_CreatesTheDevice()
    {
        var result = await _register.ExecuteAsync("user-1", new RegisterDeviceRequest(Token, "android"));

        result.Created.Should().BeTrue();
        result.Device.Platform.Should().Be("android");
        _devices.Verify(r => r.SaveAsync(It.Is<DeviceToken>(d => d.UserId == "user-1" && d.Token == Token)), Times.Once);
    }

    [Fact]
    public async Task Register_NormalizesThePlatformAndTrimsTheToken()
    {
        await _register.ExecuteAsync("user-1", new RegisterDeviceRequest($"  {Token}  ", "  Android "));

        _devices.Verify(r => r.SaveAsync(It.Is<DeviceToken>(d => d.Token == Token && d.Platform == "android")), Times.Once);
    }

    [Fact]
    public async Task Register_TheSameTokenForTheSameUser_IsIdempotentAndOnlyRefreshesLastSeen()
    {
        var existing = DeviceToken.Create("user-1", Token, "android");
        _devices.Setup(r => r.GetByTokenAsync(Token)).ReturnsAsync(existing);

        var result = await _register.ExecuteAsync("user-1", new RegisterDeviceRequest(Token, "android"));

        result.Created.Should().BeFalse();
        result.Device.Id.Should().Be(existing.Id);
        _devices.Verify(r => r.SaveAsync(It.Is<DeviceToken>(d => d.Id == existing.Id)), Times.Once);
    }

    [Fact]
    public async Task Register_ATokenThatBelongedToAnotherUser_MovesToTheCaller()
    {
        var existing = DeviceToken.Create("previous-owner", Token, "android");
        _devices.Setup(r => r.GetByTokenAsync(Token)).ReturnsAsync(existing);

        await _register.ExecuteAsync("user-2", new RegisterDeviceRequest(Token, "android"));

        existing.UserId.Should().Be("user-2");
        _devices.Verify(r => r.SaveAsync(existing), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")]
    public async Task Register_RejectsEmptyOrTooShortTokens(string token)
    {
        var act = () => _register.ExecuteAsync("user-1", new RegisterDeviceRequest(token, "android"));

        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task Register_RejectsAbsurdlyLongTokens()
    {
        var act = () => _register.ExecuteAsync("user-1", new RegisterDeviceRequest(new string('x', 5000), "android"));

        await act.Should().ThrowAsync<BusinessException>();
    }

    [Theory]
    [InlineData("windows")]
    [InlineData("")]
    [InlineData("<script>")]
    public async Task Register_RejectsUnknownPlatforms(string platform)
    {
        var act = () => _register.ExecuteAsync("user-1", new RegisterDeviceRequest(Token, platform));

        await act.Should().ThrowAsync<BusinessException>();
    }

    [Theory]
    [InlineData("android")]
    [InlineData("ios")]
    [InlineData("web")]
    public async Task Register_AcceptsTheSupportedPlatforms(string platform)
    {
        var result = await _register.ExecuteAsync("user-1", new RegisterDeviceRequest(Token, platform));

        result.Device.Platform.Should().Be(platform);
    }

    [Fact]
    public async Task Unregister_RemovesTheCallersDevice()
    {
        var device = DeviceToken.Create("user-1", Token, "android");
        _devices.Setup(r => r.GetByIdAsync(device.Id)).ReturnsAsync(device);

        await _unregister.ExecuteAsync("user-1", device.Id);

        _devices.Verify(r => r.DeleteAsync(device.Id), Times.Once);
    }

    [Fact]
    public async Task Unregister_ADeviceOfAnotherUserOrMissing_ThrowsNotFoundAndDeletesNothing()
    {
        var device = DeviceToken.Create("owner", Token, "android");
        _devices.Setup(r => r.GetByIdAsync(device.Id)).ReturnsAsync(device);

        var other = () => _unregister.ExecuteAsync("intruder", device.Id);
        var missing = () => _unregister.ExecuteAsync("user-1", "nope");

        await other.Should().ThrowAsync<DeviceTokenNotFoundException>();
        await missing.Should().ThrowAsync<DeviceTokenNotFoundException>();
        _devices.Verify(r => r.DeleteAsync(It.IsAny<string>()), Times.Never);
    }
}
