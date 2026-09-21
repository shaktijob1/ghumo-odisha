namespace GhumoOdisha.Application.Invoices;

public interface IInvoiceService
{
    /// <summary>Throws NotFoundException if the booking doesn't exist or isn't the caller's,
    /// ConflictException if it hasn't been confirmed yet (nothing to invoice for a mere request).</summary>
    Task<byte[]> GenerateInvoicePdfAsync(int customerId, int bookingId, CancellationToken cancellationToken = default);
}
