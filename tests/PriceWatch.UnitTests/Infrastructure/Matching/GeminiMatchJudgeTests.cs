using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.ValueObjects;
using PriceWatch.Infrastructure.Matching;
using PriceWatch.Infrastructure.Settings;
using Xunit;

namespace PriceWatch.UnitTests.Infrastructure.Matching;

public class GeminiMatchJudgeTests
{
    private readonly Product _product = Product.Create("Samsung Galaxy S23 FE 128GB", brand: "Samsung", model: "S23 FE");

    private static GeminiMatchJudge Build(FakeHandler handler, string? apiKey = "chave-de-teste") =>
        new(new HttpClient(handler), Options.Create(new GeminiSettings { ApiKey = apiKey }), NullLogger<GeminiMatchJudge>.Instance);

    private static string GeminiBody(string innerJson) =>
        JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text = innerJson } } } } } });

    [Fact]
    public void WithoutAnApiKey_IsNotAvailableAndNeverCallsTheApi()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        var judge = Build(handler, apiKey: null);

        judge.IsAvailable.Should().BeFalse();
        judge.JudgeAsync(_product, [new OfferSearchResult("u", "n", 1m)]).Result.Should().BeNull();
        judge.SuggestSearchQueryAsync(_product).Result.Should().BeNull();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task JudgeAsync_ParsesOneVerdictPerCandidateAndSendsTheKeyInTheHeader()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, GeminiBody(
            """{"veredictos":[{"indice":0,"mesmoProduto":false,"motivo":"S21 FE, outra geração"},{"indice":1,"mesmoProduto":true,"motivo":"Mesmo modelo"}]}"""));

        var verdicts = await Build(handler).JudgeAsync(_product,
            [new OfferSearchResult("u1", "Samsung Galaxy S21 FE 128GB", 1m), new OfferSearchResult("u2", "Samsung Galaxy S23 FE 128GB", 1m)]);

        verdicts.Should().HaveCount(2);
        verdicts![0].SameProduct.Should().BeFalse();
        verdicts[1].SameProduct.Should().BeTrue();
        verdicts[1].Reason.Should().Be("Mesmo modelo");
        handler.LastKeyHeader.Should().Be("chave-de-teste");
        handler.LastUri!.AbsoluteUri.Should().Contain("generativelanguage.googleapis.com").And.NotContain("chave-de-teste");
        handler.LastBody.Should().Contain("Samsung Galaxy S21 FE 128GB").And.NotContain("chave-de-teste");
    }

    [Fact]
    public async Task JudgeAsync_IgnoresVerdictsWithAnIndexOutsideTheList()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, GeminiBody(
            """{"veredictos":[{"indice":7,"mesmoProduto":true,"motivo":"x"},{"indice":0,"mesmoProduto":true,"motivo":"ok"}]}"""));

        var verdicts = await Build(handler).JudgeAsync(_product, [new OfferSearchResult("u1", "n", 1m)]);

        verdicts.Should().ContainSingle().Which.Index.Should().Be(0);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "{}")]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, "isso não é json")]
    public async Task JudgeAsync_OnFailureOrGarbage_ReturnsNullInsteadOfThrowing(HttpStatusCode status, string body)
    {
        var verdicts = await Build(new FakeHandler(status, body)).JudgeAsync(_product, [new OfferSearchResult("u1", "n", 1m)]);

        verdicts.Should().BeNull();
    }

    [Fact]
    public async Task JudgeAsync_WhenTheRequestThrows_ReturnsNull()
    {
        var verdicts = await Build(new FakeHandler(throws: true)).JudgeAsync(_product, [new OfferSearchResult("u1", "n", 1m)]);

        verdicts.Should().BeNull();
    }

    [Fact]
    public async Task SuggestSearchQueryAsync_ReturnsTheTrimmedQuery()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, GeminiBody("""{"busca":"  samsung galaxy s23 fe 128gb  "}"""));

        (await Build(handler).SuggestSearchQueryAsync(_product)).Should().Be("samsung galaxy s23 fe 128gb");
    }

    [Fact]
    public void ParseQuery_CapsTheLengthAndRejectsEmptyAnswers()
    {
        GeminiMatchJudge.ParseQuery($$"""{"busca":"{{new string('a', 300)}}"}""")!.Length.Should().BeLessThanOrEqualTo(80);
        GeminiMatchJudge.ParseQuery("""{"busca":"   "}""").Should().BeNull();
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        private readonly bool _throws;

        public FakeHandler(HttpStatusCode status = HttpStatusCode.OK, string body = "{}", bool throws = false)
        {
            _status = status;
            _body = body;
            _throws = throws;
        }

        public int Calls { get; private set; }
        public string? LastKeyHeader { get; private set; }
        public string? LastBody { get; private set; }
        public Uri? LastUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (_throws)
                throw new HttpRequestException("rede fora do ar");

            LastUri = request.RequestUri;
            LastKeyHeader = request.Headers.TryGetValues("x-goog-api-key", out var values) ? values.Single() : null;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") };
        }
    }
}
