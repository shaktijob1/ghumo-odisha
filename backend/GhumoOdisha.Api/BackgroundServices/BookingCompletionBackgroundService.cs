using GhumoOdisha.Application.Bookings;

namespace GhumoOdisha.Api.BackgroundServices;

/// <summary>
/// Promotes Confirmed bookings to Completed once their date slot's end date has passed.
/// </summary>
public class BookingCompletionBackgroundService(IServiceScopeFactory scopeFactory, ILogger<BookingCompletionBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
                var promoted = await bookingService.PromoteCompletedBookingsAsync(stoppingToken);
                if (promoted > 0)
                {
                    logger.LogInformation("Promoted {Count} booking(s) to Completed.", promoted);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to promote completed bookings.");
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
