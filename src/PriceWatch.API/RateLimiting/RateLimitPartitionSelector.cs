using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace PriceWatch.API.RateLimiting;

public static class RateLimitPartitionSelector
{
    public static RateLimitPartition<string> Select(
        HttpContext httpContext,
        int anonymousPermitLimit,
        int authenticatedPermitLimit,
        TimeSpan window)
    {
        var isAuthenticated = httpContext.User.Identity?.IsAuthenticated ?? false;

        if (isAuthenticated)
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter($"user:{userId}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authenticatedPermitLimit,
                Window = window,
                QueueLimit = 0
            });
        }

        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"ip:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = anonymousPermitLimit,
            Window = window,
            QueueLimit = 0
        });
    }
}
