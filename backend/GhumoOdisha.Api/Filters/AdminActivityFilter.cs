using System.Text.RegularExpressions;
using GhumoOdisha.Api.Logging;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace GhumoOdisha.Api.Filters;

/// <summary>
/// Records every change an admin makes — any non-GET call to an Admin*Controller (except login) —
/// in AdminActivities: who, what, on which record, and whether it succeeded. Automatic, so a new
/// admin endpoint is audited without anyone remembering to add a log line.
/// </summary>
public partial class AdminActivityFilter(IServiceScopeFactory scopeFactory, ILogger<AdminActivityFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        if (context.ActionDescriptor is not ControllerActionDescriptor action
            || !action.ControllerName.StartsWith("Admin", StringComparison.Ordinal)
            || action.ControllerName == "AdminAuth"
            || HttpMethods.IsGet(context.HttpContext.Request.Method))
        {
            return;
        }

        var (adminId, role) = UserLogContextMiddleware.CallerOf(context.HttpContext.User);
        if (adminId is null || role != "Admin")
        {
            return;
        }

        var status = executed.Exception switch
        {
            null => (executed.Result as IStatusCodeActionResult)?.StatusCode ?? 200,
            AppException app => (int)app.StatusCode,
            _ => 500
        };

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GhumoOdishaDbContext>();
            db.AdminActivities.Add(new AdminActivity
            {
                AdminUserId = adminId.Value,
                Action = Humanize(action.ActionName),
                Area = Humanize(action.ControllerName["Admin".Length..]),
                TargetId = TargetIdOf(context),
                HttpMethod = context.HttpContext.Request.Method,
                Path = Truncate(context.HttpContext.Request.Path.Value ?? "", 300),
                StatusCode = status,
                Succeeded = status < 400,
                RequestId = context.HttpContext.TraceIdentifier,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // The admin's action already happened — failing to audit it must not turn it into an error.
            logger.LogError(ex, "Could not record admin activity for {Action}", action.ActionName);
        }
    }

    /// <summary>The record acted on: the route's "id", or the first other "...Id" route value.</summary>
    private static int? TargetIdOf(ActionExecutingContext context)
    {
        var values = context.RouteData.Values;
        var key = values.ContainsKey("id")
            ? "id"
            : values.Keys.FirstOrDefault(k => k.EndsWith("Id", StringComparison.OrdinalIgnoreCase));
        return key is not null && int.TryParse(values[key]?.ToString(), out var id) ? id : null;
    }

    /// <summary>"AddPayment" → "Add payment", "TripPhotos" → "Trip photos".</summary>
    private static string Humanize(string pascal)
    {
        var words = WordBoundary().Replace(pascal, " $1").Trim();
        return words.Length == 0 ? pascal : char.ToUpperInvariant(words[0]) + words[1..].ToLowerInvariant();
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex WordBoundary();
}
