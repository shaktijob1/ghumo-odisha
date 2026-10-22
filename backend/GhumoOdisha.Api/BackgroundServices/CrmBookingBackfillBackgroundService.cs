using GhumoOdisha.Application.Crm;
using GhumoOdisha.Infrastructure.Persistence;

namespace GhumoOdisha.Api.BackgroundServices;

/// <summary>
/// Once, shortly after start-up: puts every paid booking the CRM doesn't know about yet into Admin → CRM
/// (bookings made before the CRM existed). New bookings are added as they happen; this only catches up,
/// and does nothing once everything is linked.
/// </summary>
public class CrmBookingBackfillBackgroundService(IServiceScopeFactory scopeFactory, ILogger<CrmBookingBackfillBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let startup (migrations) finish first.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GhumoOdishaDbContext>();
            var linked = await CrmBookingSync.BackfillAsync(db, stoppingToken);
            if (linked > 0)
            {
                logger.LogInformation("CRM: added {Count} existing booking(s) to the CRM.", linked);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "CRM booking backfill failed; it will be retried on the next start, or from Admin → CRM.");
        }
    }
}
