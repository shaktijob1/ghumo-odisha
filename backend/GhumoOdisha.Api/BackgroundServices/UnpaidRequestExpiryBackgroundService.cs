using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Payments;

namespace GhumoOdisha.Api.BackgroundServices;

/// <summary>
/// Every minute, for unpaid website checkouts, in this order:
/// 1. asks Razorpay whether they were paid — a payment the browser never reported (page refreshed
///    or closed after paying) confirms the booking;
/// 2. sends the "complete your payment" reminders that are due (15 and 30 minutes in);
/// 3. cancels checkouts still unpaid after <see cref="BookingService.UnpaidRequestExpiryMinutes"/> minutes.
/// Every 10 minutes it also refunds money that reached an already-closed checkout or a second order.
/// A request never holds seats, so expiry restores nothing.
/// </summary>
public class UnpaidRequestExpiryBackgroundService(IServiceScopeFactory scopeFactory, ILogger<UnpaidRequestExpiryBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private const int ClosedSweepEveryTicks = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tick = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunStepAsync("reconcile open checkouts", async sp =>
            {
                var confirmed = await sp.GetRequiredService<IBookingPaymentService>().ReconcileOpenCheckoutsAsync(stoppingToken);
                if (confirmed > 0) logger.LogInformation("Confirmed {Count} booking(s) from Razorpay payments the browser never reported.", confirmed);
            }, stoppingToken);

            await RunStepAsync("send payment reminders", async sp =>
            {
                var reminded = await sp.GetRequiredService<IPaymentReminderService>().SendDueRemindersAsync(stoppingToken);
                if (reminded > 0) logger.LogInformation("Sent payment reminders for {Count} checkout(s).", reminded);
            }, stoppingToken);

            await RunStepAsync("expire unpaid requests", async sp =>
            {
                var expired = await sp.GetRequiredService<IBookingService>().ExpireUnpaidRequestsAsync(stoppingToken);
                if (expired > 0) logger.LogInformation("Cancelled {Count} unpaid booking request(s) older than {Minutes} minutes.", expired, BookingService.UnpaidRequestExpiryMinutes);
            }, stoppingToken);

            if (tick++ % ClosedSweepEveryTicks == 0)
            {
                await RunStepAsync("reconcile closed checkouts", sp => sp.GetRequiredService<IBookingPaymentService>().ReconcileClosedCheckoutsAsync(stoppingToken), stoppingToken);
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

    /// <summary>Each step in its own scope, so one failing never stops the others.</summary>
    private async Task RunStepAsync(string name, Func<IServiceProvider, Task> step, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            await step(scope.ServiceProvider);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Payment follow-up step failed: {Step}.", name);
        }
    }
}
