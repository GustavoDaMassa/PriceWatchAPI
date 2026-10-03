using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Infrastructure.Push;
using PriceWatch.Infrastructure.Settings;
using Xunit;

namespace PriceWatch.UnitTests.Infrastructure.Push;

public class FcmPushSenderTests
{
    private readonly Mock<IFcmAccessTokenProvider> _tokens = new();
    private HttpRequestMessage? _sent;
    private string? _sentBody;

    public FcmPushSenderTests()
    {
        _tokens.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("ya29.access");
    }

    private FcmPushSender Build(
        HttpStatusCode status = HttpStatusCode.OK,
        string body = """{"name":"projects/pw/messages/1"}""",
        bool configured = true,
        bool throwOnSend = false)
    {
        var handler = new Mock<HttpMessageHandler>();
        var setup = handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());

        if (throwOnSend)
            setup.ThrowsAsync(new HttpRequestException("network down"));
        else
            setup.Returns<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                _sent = req;
                _sentBody = await req.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            });

        var settings = new FcmSettings { ProjectId = configured ? "pw-project" : "", ServiceAccountJson = configured ? "{}" : "" };
        return new FcmPushSender(new HttpClient(handler.Object), _tokens.Object, Options.Create(settings), NullLogger<FcmPushSender>.Instance);
    }

    private static PushMessage Message() =>
        new("Preço-alvo atingido", "Notebook por R$ 2.599,00", new Dictionary<string, string> { ["productId"] = "p-1" });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsAvailable_FollowsTheFcmConfiguration(bool configured)
    {
        Build(configured: configured).IsAvailable.Should().Be(configured);
    }

    [Fact]
    public async Task Send_PostsTheMessageToTheV1EndpointWithTheBearerToken()
    {
        var result = await Build().SendAsync("device-token-abc", Message());

        result.Should().Be(PushResult.Sent);
        _sent!.Method.Should().Be(HttpMethod.Post);
        _sent.RequestUri!.ToString().Should().Be("https://fcm.googleapis.com/v1/projects/pw-project/messages:send");
        _sent.Headers.Authorization!.Parameter.Should().Be("ya29.access");

        var message = JsonDocument.Parse(_sentBody!).RootElement.GetProperty("message");
        message.GetProperty("token").GetString().Should().Be("device-token-abc");
        message.GetProperty("notification").GetProperty("title").GetString().Should().Be("Preço-alvo atingido");
        message.GetProperty("notification").GetProperty("body").GetString().Should().Be("Notebook por R$ 2.599,00");
        message.GetProperty("data").GetProperty("productId").GetString().Should().Be("p-1");
        message.GetProperty("android").GetProperty("priority").GetString().Should().Be("HIGH");
    }

    [Fact]
    public async Task Send_ATokenFirebaseDoesNotKnowAnymore_IsReportedAsInvalid()
    {
        var notFound = Build(HttpStatusCode.NotFound, """{"error":{"code":404,"status":"NOT_FOUND","details":[{"errorCode":"UNREGISTERED"}]}}""");
        var badArgument = Build(HttpStatusCode.BadRequest, """{"error":{"code":400,"status":"INVALID_ARGUMENT","message":"The registration token is not a valid FCM registration token"}}""");

        (await notFound.SendAsync("t", Message())).Should().Be(PushResult.InvalidToken);
        (await badArgument.SendAsync("t", Message())).Should().Be(PushResult.InvalidToken);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Send_OtherErrors_AreAFailureNotAnInvalidToken(HttpStatusCode status)
    {
        var result = await Build(status, """{"error":{"status":"INTERNAL"}}""").SendAsync("t", Message());

        result.Should().Be(PushResult.Failed);
    }

    [Fact]
    public async Task Send_AnUnrelated400_DoesNotDiscardTheToken()
    {
        var result = await Build(HttpStatusCode.BadRequest, """{"error":{"status":"INVALID_ARGUMENT","message":"Invalid JSON payload"}}""")
            .SendAsync("t", Message());

        result.Should().Be(PushResult.Failed);
    }

    [Fact]
    public async Task Send_NeverThrows_EvenWhenTheNetworkOrTheTokenProviderFails()
    {
        (await Build(throwOnSend: true).SendAsync("t", Message())).Should().Be(PushResult.Failed);

        _tokens.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("auth down"));
        (await Build().SendAsync("t", Message())).Should().Be(PushResult.Failed);
    }

    [Fact]
    public async Task Send_WhenNotConfigured_FailsWithoutCallingFirebase()
    {
        var sender = Build(configured: false);

        var result = await sender.SendAsync("t", Message());

        result.Should().Be(PushResult.Failed);
        _sent.Should().BeNull();
    }
}
