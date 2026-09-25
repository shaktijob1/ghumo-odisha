using System.Globalization;
using System.Text;
using System.Text.Json;
using MySqlConnector;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Parsing;

namespace GhumoOdisha.Api.Logging;

/// <summary>
/// Serilog sink behind the admin Logs screen. Writes batches straight to the AppLogs table with
/// MySqlConnector — not through EF Core, whose own command logging would otherwise feed back into
/// this sink. Keeps only what an admin needs: warnings/errors, this app's own events, and the
/// per-request summary line for API calls that involve a signed-in user, a change, or a failure.
/// A database outage never breaks the app: failures go to Serilog's SelfLog and the batch is dropped.
/// </summary>
public sealed class DatabaseLogSink(string connectionString) : IBatchedLogEventSink
{
    private const string RequestLoggingSource = "Serilog.AspNetCore.RequestLoggingMiddleware";

    // Promoted to their own columns, so they're left out of PropertiesJson.
    private static readonly HashSet<string> ColumnProperties =
    [
        LogProperties.RequestId, "RequestId", LogProperties.UserId, LogProperties.UserRole, LogProperties.ClientIp,
        "RequestMethod", "RequestPath", "StatusCode", "Elapsed", "SourceContext",
        "ConnectionId", "ActionId", "ActionName", "EventId", "TraceId", "SpanId", "ParentId"
    ];

    private const int MaxMessageLength = 4000;
    private const int MaxExceptionLength = 60000;

    public async Task EmitBatchAsync(IReadOnlyCollection<LogEvent> batch)
    {
        var rows = batch.Where(ShouldStore).ToList();
        if (rows.Count == 0)
        {
            return;
        }

        try
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var sql = new StringBuilder(
                "INSERT INTO AppLogs (TimestampUtc, Level, Message, Exception, Source, RequestId, RequestMethod, RequestPath, " +
                "StatusCode, ElapsedMs, UserId, UserRole, ClientIp, PropertiesJson) VALUES ");
            await using var command = connection.CreateCommand();

            for (var i = 0; i < rows.Count; i++)
            {
                var e = rows[i];
                if (i > 0) sql.Append(',');
                sql.Append($"(@t{i},@l{i},@m{i},@x{i},@s{i},@r{i},@rm{i},@rp{i},@sc{i},@el{i},@u{i},@ur{i},@ip{i},@p{i})");

                command.Parameters.AddWithValue($"@t{i}", e.Timestamp.UtcDateTime);
                command.Parameters.AddWithValue($"@l{i}", e.Level.ToString());
                command.Parameters.AddWithValue($"@m{i}", Truncate(RenderMessage(e), MaxMessageLength));
                command.Parameters.AddWithValue($"@x{i}", e.Exception is null ? DBNull.Value : Truncate(e.Exception.ToString(), MaxExceptionLength));
                command.Parameters.AddWithValue($"@s{i}", (object?)Truncate(Scalar(e, "SourceContext"), 200) ?? DBNull.Value);
                command.Parameters.AddWithValue($"@r{i}", (object?)Truncate(Scalar(e, LogProperties.RequestId), 16) ?? DBNull.Value);
                command.Parameters.AddWithValue($"@rm{i}", (object?)Truncate(Scalar(e, "RequestMethod"), 10) ?? DBNull.Value);
                command.Parameters.AddWithValue($"@rp{i}", (object?)Truncate(Scalar(e, "RequestPath"), 300) ?? DBNull.Value);
                command.Parameters.AddWithValue($"@sc{i}", (object?)IntScalar(e, "StatusCode") ?? DBNull.Value);
                command.Parameters.AddWithValue($"@el{i}", (object?)DoubleScalar(e, "Elapsed") ?? DBNull.Value);
                command.Parameters.AddWithValue($"@u{i}", (object?)IntScalar(e, LogProperties.UserId) ?? DBNull.Value);
                command.Parameters.AddWithValue($"@ur{i}", (object?)Truncate(Scalar(e, LogProperties.UserRole), 20) ?? DBNull.Value);
                command.Parameters.AddWithValue($"@ip{i}", (object?)Truncate(Scalar(e, LogProperties.ClientIp), 45) ?? DBNull.Value);
                command.Parameters.AddWithValue($"@p{i}", (object?)PropertiesJson(e) ?? DBNull.Value);
            }

            command.CommandText = sql.ToString();
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("DatabaseLogSink failed to write {0} log events: {1}", rows.Count, ex);
        }
    }

    public Task OnEmptyBatchAsync() => Task.CompletedTask;

    public static bool ShouldStore(LogEvent e)
    {
        var source = Scalar(e, "SourceContext") ?? "";

        // EF Core's own chatter (and anything it might log about this sink's writes) stays out,
        // unless it's an actual error.
        if (source.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
        {
            return e.Level >= LogEventLevel.Error;
        }

        if (source == RequestLoggingSource)
        {
            var path = Scalar(e, "RequestPath") ?? "";
            if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var status = IntScalar(e, "StatusCode") ?? 0;
            var method = Scalar(e, "RequestMethod") ?? "GET";

            // Browser CORS pre-checks carry no meaning of their own.
            if (string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                return status >= 500;
            }

            if (status >= 400 || !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                return true; // Every failure and every change tells part of a story.
            }

            // Successful reads: keep a signed-in customer's (their journey), skip anonymous browsing
            // and admin screen loads — which would otherwise include the Logs screen logging itself.
            return Scalar(e, LogProperties.UserRole) == "Customer";
        }

        return e.Level >= LogEventLevel.Warning
            || (e.Level >= LogEventLevel.Information && source.StartsWith("GhumoOdisha", StringComparison.Ordinal));
    }

    /// <summary>Renders the message with string values unquoted — easier to read than Serilog's default.</summary>
    private static string RenderMessage(LogEvent e)
    {
        var sb = new StringBuilder();
        foreach (var token in e.MessageTemplate.Tokens)
        {
            if (token is PropertyToken pt && e.Properties.TryGetValue(pt.PropertyName, out var value)
                && value is ScalarValue { Value: string s })
            {
                sb.Append(s);
            }
            else
            {
                using var writer = new StringWriter(sb, CultureInfo.InvariantCulture);
                token.Render(e.Properties, writer, CultureInfo.InvariantCulture);
            }
        }

        return sb.ToString();
    }

    private static string? PropertiesJson(LogEvent e)
    {
        var extra = e.Properties
            .Where(p => !ColumnProperties.Contains(p.Key))
            .ToDictionary(p => p.Key, p => p.Value is ScalarValue sv ? sv.Value : (object?)p.Value.ToString());
        return extra.Count == 0 ? null : JsonSerializer.Serialize(extra);
    }

    private static string? Scalar(LogEvent e, string name) =>
        e.Properties.TryGetValue(name, out var v) && v is ScalarValue { Value: not null } sv
            ? Convert.ToString(sv.Value, CultureInfo.InvariantCulture)
            : null;

    private static int? IntScalar(LogEvent e, string name) =>
        int.TryParse(Scalar(e, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static double? DoubleScalar(LogEvent e, string name) =>
        double.TryParse(Scalar(e, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? Math.Round(d, 1) : null;

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}

/// <summary>Names of the request-context properties every log line carries.</summary>
public static class LogProperties
{
    // Not "RequestId": ASP.NET Core adds its own RequestId (a connection-based id) to every ILogger
    // line and it would override ours, so the customer-facing ref gets a name nothing else uses.
    public const string RequestId = "ErrorRef";
    public const string UserId = "UserId";
    public const string UserRole = "UserRole";
    public const string ClientIp = "ClientIp";
}
