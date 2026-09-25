using System.Net;
using System.Text.Json;
using GhumoOdisha.Api.Logging;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using Serilog.Context;

namespace GhumoOdisha.Api.Middleware;

/// <summary>
/// Turns exceptions into the standard error envelope. Unexpected errors are logged in full (server
/// side only — never sent to the client) and the client gets an "Error ref" it can quote to support;
/// expected business rejections ("only 2 seats left") are logged too, so the admin Logs screen shows
/// why a request failed, not just that it did.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            // This runs outside the authenticated part of the pipeline, so re-attach who the caller was.
            var (userId, role) = UserLogContextMiddleware.CallerOf(context.User);
            using (LogContext.PushProperty(LogProperties.UserId, userId))
            using (LogContext.PushProperty(LogProperties.UserRole, role))
            {
                await HandleAsync(context, ex);
            }
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        switch (exception)
        {
            case ValidationAppException ex:
                logger.LogInformation("Request rejected by validation on {Method} {Path}: {Errors}",
                    context.Request.Method, context.Request.Path.Value, string.Join(" | ", ex.Errors));
                await WriteResponseAsync(context, HttpStatusCode.BadRequest, ApiResponse<object>.Fail(ex.Message, ex.Errors));
                break;

            case AppException ex:
                logger.LogInformation("Request rejected ({StatusCode}) on {Method} {Path}: {Reason}",
                    (int)ex.StatusCode, context.Request.Method, context.Request.Path.Value, ex.Message);
                await WriteResponseAsync(context, ex.StatusCode, ApiResponse<object>.Fail(ex.Message));
                break;

            default:
                logger.LogError(exception, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path.Value);
                await WriteResponseAsync(context, HttpStatusCode.InternalServerError,
                    ApiResponse<object>.Fail($"An unexpected error occurred. Error ref: {context.TraceIdentifier}"));
                break;
        }
    }

    private static Task WriteResponseAsync(HttpContext context, HttpStatusCode statusCode, ApiResponse<object> body)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
