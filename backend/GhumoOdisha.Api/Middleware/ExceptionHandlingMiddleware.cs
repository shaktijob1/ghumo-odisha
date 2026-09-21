using System.Net;
using System.Text.Json;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;

namespace GhumoOdisha.Api.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationAppException ex)
        {
            await WriteResponseAsync(context, HttpStatusCode.BadRequest, ApiResponse<object>.Fail(ex.Message, ex.Errors));
        }
        catch (AppException ex)
        {
            await WriteResponseAsync(context, ex.StatusCode, ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteResponseAsync(context, HttpStatusCode.InternalServerError, ApiResponse<object>.Fail("An unexpected error occurred."));
        }
    }

    private static Task WriteResponseAsync(HttpContext context, HttpStatusCode statusCode, ApiResponse<object> body)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
