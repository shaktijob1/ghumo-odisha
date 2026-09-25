namespace GhumoOdisha.Application.Invoices;

/// <summary>
/// Invoices are never stored — each download renders a fresh PDF from the current booking and its
/// payment rows, so it always reflects every payment recorded so far.
/// </summary>
public interface IInvoiceService
{
    /// <summary>Throws NotFoundException if the booking doesn't exist or isn't the caller's,
    /// ConflictException if it hasn't been confirmed yet (nothing to invoice for a mere request).</summary>
    Task<byte[]> GenerateInvoicePdfAsync(int customerId, int bookingId, CancellationToken cancellationToken = default);

    /// <summary>Same invoice, for the admin — any customer's booking.</summary>
    Task<byte[]> GenerateAdminInvoicePdfAsync(int bookingId, CancellationToken cancellationToken = default);
}
