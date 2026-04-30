using Microsoft.AspNetCore.Diagnostics;
using PriceWatch.API.Errors;
using PriceWatch.Domain.Exceptions;

namespace PriceWatch.API.Middleware;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, error) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            BusinessException => (StatusCodes.Status400BadRequest, "Bad Request"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new ErrorResponse(status, error, exception.Message),
            cancellationToken);

        return true;
    }
}
