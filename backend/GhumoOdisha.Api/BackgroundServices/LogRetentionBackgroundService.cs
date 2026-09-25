using GhumoOdisha.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Api.BackgroundServices;

/// <summary>
/// Once a day, deletes stored logs older than Logging:RetentionDays (default 180 — the CERT-In
/// minimum) and admin activity older than Logging:AdminActivityRetentionDays (default 730).
/// Deletes in chunks so a large backlog never locks the table for long.
/// </summary>
public class LogRetentionBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<LogRetentionBackgroundService> logger)
    : BackgroundService
{
    private const int ChunkSize = 5000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let startup (migrations) finish first.
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Log retention cleanup failed.");
            }

            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        var logDays = configuration.GetValue("Logging:RetentionDays", 180);
        var activityDays = configuration.GetValue("Logging:AdminActivityRetentionDays", 730);
        var logCutoff = DateTime.UtcNow.AddDays(-logDays);
        var activityCutoff = DateTime.UtcNow.AddDays(-activityDays);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GhumoOdishaDbContext>();

        int deleted, total = 0;
        do
        {
            deleted = await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM AppLogs WHERE TimestampUtc < {logCutoff} ORDER BY AppLogId LIMIT {ChunkSize}", cancellationToken);
            total += deleted;
        } while (deleted == ChunkSize && !cancellationToken.IsCancellationRequested);

        var activity = await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM AdminActivities WHERE CreatedAtUtc < {activityCutoff}", cancellationToken);

        if (total > 0 || activity > 0)
        {
            logger.LogInformation("Log retention: removed {Logs} log entries older than {LogDays} days and {Activity} admin activity rows older than {ActivityDays} days.",
                total, logDays, activity, activityDays);
        }
    }
}
