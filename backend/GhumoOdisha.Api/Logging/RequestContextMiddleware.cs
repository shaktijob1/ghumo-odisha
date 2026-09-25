using System.Security.Claims;
using Serilog.Context;

namespace GhumoOdisha.Api.Logging;

/// <summary>
/// First in the pipeline: gives every request a short id (returned as the X-Request-Id header and
/// shown to users as "Error ref") and tags every log line written during the request with it and
/// the caller's IP.
/// </summary>
public class RequestIdMiddleware(RequestDelegate next)
{
    // No 0/O/1/I — easy to read out over the phone.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = NewId();
        context.TraceIdentifier = requestId;
        context.Response.Headers["X-Request-Id"] = requestId;

        using (LogContext.PushProperty(LogProperties.RequestId, requestId))
        using (LogContext.PushProperty(LogProperties.ClientIp, context.Connection.RemoteIpAddress?.ToString()))
        {
            await next(context);
        }
    }

    private static string NewId() => string.Create(8, 0, (span, _) =>
    {
        for (var i = 0; i < span.Length; i++)
        {
            span[i] = Alphabet[Random.Shared.Next(Alphabet.Length)];
        }
    });
}

/// <summary>
/// Runs right after authentication: tags every log line written during the request with who made
/// it (customer or admin id), so the Logs screen can answer "which user did this happen to".
/// </summary>
public class UserLogContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var (userId, role) = CallerOf(context.User);
        if (userId is null)
        {
            await next(context);
            return;
        }

        using (LogContext.PushProperty(LogProperties.UserId, userId))
        using (LogContext.PushProperty(LogProperties.UserRole, role))
        {
            await next(context);
        }
    }

    /// <summary>The signed-in caller's id and role, from the JWT ("sub" + role claim).</summary>
    public static (int? UserId, string? Role) CallerOf(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return (null, null);
        }

        var sub = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(sub, out var id))
        {
            return (null, null);
        }

        var role = user.IsInRole("Admin") ? "Admin" : user.IsInRole("Customer") ? "Customer" : null;
        return (id, role);
    }
}
