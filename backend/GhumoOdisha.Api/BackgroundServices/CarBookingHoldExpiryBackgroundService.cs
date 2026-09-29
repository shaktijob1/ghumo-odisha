using GhumoOdisha.Application.Cars;

namespace GhumoOdisha.Api.BackgroundServices;

/// <summary>
/// Marks unpaid car bookings whose payment hold ran out as Expired. Housekeeping only — availability
/// checks already ignore expired holds, so a late run never blocks a car.
/// </summary>
public class CarBookingHoldExpiryBackgroundService(IServiceScopeFactory scopeFactory, ILogger<CarBookingHoldExpiryBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var expired = await scope.ServiceProvider.GetRequiredService<ICarBookingService>().ExpireStaleHoldsAsync(stoppingToken);
                if (expired > 0)
                {
                    logger.LogInformation("Expired {Count} unpaid car booking hold(s).", expired);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to expire unpaid car booking holds.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
