namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// One application log event kept for the admin Logs screen: every warning/error, the app's own
/// business events, and a one-line summary of each API request that matters (signed-in users,
/// changes, failures). Written by the Serilog database sink, never by application code directly.
/// </summary>
public class AppLog
{
    public long AppLogId { get; set; }
    public DateTime TimestampUtc { get; set; }

    /// <summary>Information / Warning / Error / Fatal.</summary>
    public string Level { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? Exception { get; set; }

    /// <summary>The class that wrote the log, e.g. GhumoOdisha.Application.Payments.BookingPaymentService.</summary>
    public string? Source { get; set; }

    /// <summary>Short id shared by every log line of one HTTP request — also shown to users as "Error ref".</summary>
    public string? RequestId { get; set; }
    public string? RequestMethod { get; set; }
    public string? RequestPath { get; set; }
    public int? StatusCode { get; set; }
    public double? ElapsedMs { get; set; }

    public int? UserId { get; set; }

    /// <summary>"Customer" or "Admin" — which table <see cref="UserId"/> refers to.</summary>
    public string? UserRole { get; set; }
    public string? ClientIp { get; set; }

    /// <summary>Remaining structured properties as JSON (booking id, payment id, …) for the detail view.</summary>
    public string? PropertiesJson { get; set; }
}
