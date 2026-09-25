using GhumoOdisha.Api.Logging;
using GhumoOdisha.Application.Logs;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Tests.Fixtures;
using Serilog.Events;
using Serilog.Parsing;

namespace GhumoOdisha.Tests;

/// <summary>What gets stored for the admin Logs screen, and how it can be searched.</summary>
public class LoggingTests
{
    private static LogEvent Event(LogEventLevel level, string source, params (string Key, object Value)[] props)
    {
        var properties = props.Select(p => new LogEventProperty(p.Key, new ScalarValue(p.Value))).ToList();
        properties.Add(new LogEventProperty("SourceContext", new ScalarValue(source)));
        return new LogEvent(DateTimeOffset.UtcNow, level, null, new MessageTemplateParser().Parse("test"), properties);
    }

    private const string RequestSource = "Serilog.AspNetCore.RequestLoggingMiddleware";

    [Fact]
    public void Sink_KeepsWhatMatters_AndSkipsNoise()
    {
        // Always: warnings/errors, and this app's own business events.
        Assert.True(DatabaseLogSink.ShouldStore(Event(LogEventLevel.Warning, "Microsoft.AspNetCore.Something")));
        Assert.True(DatabaseLogSink.ShouldStore(Event(LogEventLevel.Information, "GhumoOdisha.Application.Payments.BookingPaymentService")));

        // Requests: signed-in users, changes and failures are stored…
        Assert.True(DatabaseLogSink.ShouldStore(Event(LogEventLevel.Information, RequestSource,
            ("RequestPath", "/api/customer/bookings"), ("RequestMethod", "GET"), ("StatusCode", 200), ("UserId", 12), ("UserRole", "Customer"))));
        Assert.True(DatabaseLogSink.ShouldStore(Event(LogEventLevel.Information, RequestSource,
            ("RequestPath", "/api/admin/bookings/5/payments"), ("RequestMethod", "POST"), ("StatusCode", 200), ("UserId", 1), ("UserRole", "Admin"))));
        Assert.True(DatabaseLogSink.ShouldStore(Event(LogEventLevel.Information, RequestSource,
            ("RequestPath", "/api/customer/bookings"), ("RequestMethod", "POST"), ("StatusCode", 200))));
        Assert.True(DatabaseLogSink.ShouldStore(Event(LogEventLevel.Warning, RequestSource,
            ("RequestPath", "/api/trips/99"), ("RequestMethod", "GET"), ("StatusCode", 404))));

        // …anonymous successful browsing, non-API paths and EF Core chatter are not.
        Assert.False(DatabaseLogSink.ShouldStore(Event(LogEventLevel.Information, RequestSource,
            ("RequestPath", "/api/trips"), ("RequestMethod", "GET"), ("StatusCode", 200))));
        Assert.False(DatabaseLogSink.ShouldStore(Event(LogEventLevel.Information, RequestSource,
            ("RequestPath", "/trips/1"), ("RequestMethod", "GET"), ("StatusCode", 200), ("UserId", 12))));
        // Admin screen loads (incl. the Logs screen reading itself) and CORS pre-checks are noise too.
        Assert.False(DatabaseLogSink.ShouldStore(Event(LogEventLevel.Information, RequestSource,
            ("RequestPath", "/api/admin/logs"), ("RequestMethod", "GET"), ("StatusCode", 200), ("UserId", 1), ("UserRole", "Admin"))));
        Assert.False(DatabaseLogSink.ShouldStore(Event(LogEventLevel.Information, RequestSource,
            ("RequestPath", "/api/admin/logs/summary"), ("RequestMethod", "OPTIONS"), ("StatusCode", 204))));
        Assert.False(DatabaseLogSink.ShouldStore(Event(LogEventLevel.Warning, "Microsoft.EntityFrameworkCore.Query")));
        Assert.False(DatabaseLogSink.ShouldStore(Event(LogEventLevel.Information, "Microsoft.Hosting.Lifetime")));
    }

    [Fact]
    public async Task ErrorRef_FindsTheWholeRequestTrail_WithTheCustomerNamed()
    {
        await using var db = TestDb.CreateContext();
        var now = DateTime.UtcNow;
        var customer = new Customer { Name = "Log Test Customer", PhoneNumber = TestDb.RandomPhoneNumber(), IsVerified = true, CreatedAt = now, UpdatedAt = now };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        // A request id no other test run will produce.
        var requestId = string.Concat(Enumerable.Range(0, 8).Select(_ => "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"[Random.Shared.Next(32)]));
        db.AppLogs.AddRange(
            new AppLog { TimestampUtc = now, Level = "Information", Message = "Payment verify attempt: booking 7", Source = "GhumoOdisha.Application.Payments.BookingPaymentService", RequestId = requestId, UserId = customer.CustomerId, UserRole = "Customer" },
            new AppLog { TimestampUtc = now.AddMilliseconds(5), Level = "Error", Message = "Unhandled exception processing POST /api/customer/bookings/7/payments/verify", Exception = "System.InvalidOperationException: boom", Source = "GhumoOdisha.Api.Middleware.ExceptionHandlingMiddleware", RequestId = requestId, UserId = customer.CustomerId, UserRole = "Customer" },
            new AppLog { TimestampUtc = now.AddMilliseconds(9), Level = "Error", Message = "POST /api/customer/bookings/7/payments/verify responded 500 in 12 ms", Source = "Serilog.AspNetCore.RequestLoggingMiddleware", RequestId = requestId, RequestMethod = "POST", RequestPath = "/api/customer/bookings/7/payments/verify", StatusCode = 500, ElapsedMs = 12, UserId = customer.CustomerId, UserRole = "Customer" });
        await db.SaveChangesAsync();

        var service = new LogQueryService(db);

        // The customer quotes the ref in lower case — still found.
        var byRef = await service.GetLogsAsync(new LogFilter(Search: requestId.ToLowerInvariant()), 1, 50);
        Assert.Equal(3, byRef.TotalCount);
        Assert.All(byRef.Items, i => Assert.StartsWith("Log Test Customer · …", i.UserLabel));

        var error = byRef.Items.Single(i => i.Source == "ExceptionHandlingMiddleware");
        Assert.True(error.HasException);
        var detail = await service.GetLogAsync(error.AppLogId);
        Assert.Equal("System.InvalidOperationException: boom", detail.Exception);
        Assert.Equal(["Information", "Error", "Error"], detail.RequestTrail.Select(t => t.Level).ToArray());

        // Searching by the customer's phone finds their logs; the Error filter narrows to errors.
        var byPhone = await service.GetLogsAsync(new LogFilter(Search: customer.PhoneNumber, Level: "Error"), 1, 50);
        Assert.Equal(2, byPhone.TotalCount);

        var summary = await service.GetSummaryAsync();
        Assert.True(summary.Errors24h >= 2);
        Assert.Contains(summary.TopFailing, f => f.Path == "/api/customer/bookings/7/payments/verify");
    }
}
