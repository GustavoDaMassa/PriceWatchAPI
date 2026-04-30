using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using PriceWatch.API.Middleware;
using PriceWatch.Domain.Exceptions;
using Xunit;

namespace PriceWatch.IntegrationTests.Middleware;

public class GlobalExceptionHandlerTests
{
    private class TestNotFoundException : NotFoundException
    {
        public TestNotFoundException(string message = "resource not found") : base(message) { }
    }

    private static GlobalExceptionHandler CreateHandler() =>
        new(NullLogger<GlobalExceptionHandler>.Instance);

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Fact]
    public async Task TryHandleAsync_NotFoundException_Returns404AndTrue()
    {
        var handler = CreateHandler();
        var context = CreateContext();

        var handled = await handler.TryHandleAsync(context, new TestNotFoundException(), CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task TryHandleAsync_BusinessException_Returns400()
    {
        var handler = CreateHandler();
        var context = CreateContext();

        await handler.TryHandleAsync(context, new BusinessException("bad request"), CancellationToken.None);

        context.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task TryHandleAsync_GenericException_Returns500()
    {
        var handler = CreateHandler();
        var context = CreateContext();

        await handler.TryHandleAsync(context, new Exception("unexpected error"), CancellationToken.None);

        context.Response.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsJsonBodyWithErrorResponse()
    {
        var handler = CreateHandler();
        var context = CreateContext();

        await handler.TryHandleAsync(context, new TestNotFoundException("item not found"), CancellationToken.None);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty("status").GetInt32().Should().Be(404);
        doc.RootElement.GetProperty("error").GetString().Should().Be("Not Found");
        doc.RootElement.GetProperty("message").GetString().Should().Be("item not found");
    }
}
