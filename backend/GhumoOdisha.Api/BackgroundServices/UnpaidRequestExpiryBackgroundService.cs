using GhumoOdisha.Application.Bookings;

namespace GhumoOdisha.Api.BackgroundServices;

/// <summary>
/// Cancels website booking requests that are still unpaid 24 hours after they were made
/// (<see cref="BookingService.UnpaidRequestExpiryHours"/>). A request never holds seats, so this only
/// tidies the admin's queue and frees the date for deletion — nothing is restored or refunded.
/// </summary>
public class UnpaidRequestExpiryBackgroundService(IServiceScopeFactory scopeFactory, ILogger<UnpaidRequestExpiryBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
                var expired = await bookingService.ExpireUnpaidRequestsAsync(stoppingToken);
                if (expired > 0)
                {
                    logger.LogInformation("Cancelled {Count} unpaid booking request(s) older than {Hours} hours.", expired, BookingService.UnpaidRequestExpiryHours);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to expire unpaid booking requests.");
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
