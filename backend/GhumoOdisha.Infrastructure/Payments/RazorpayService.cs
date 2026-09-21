using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Payments;
using GhumoOdisha.Application.Payments.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Infrastructure.Payments;

/// <summary>
/// Talks to Razorpay's REST API directly over HttpClient (no official first-party .NET SDK is
/// wired in) using HTTP Basic Auth with key_id:key_secret, same call shape as the Standard
/// Checkout "Orders API" flow: https://razorpay.com/docs/api/orders/
/// </summary>
public class RazorpayService(HttpClient httpClient, IOptions<RazorpayOptions> options, ILogger<RazorpayService> logger) : IRazorpayService
{
    private const string OrdersUrl = "https://api.razorpay.com/v1/orders";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RazorpayOptions _options = options.Value;

    public async Task<RazorpayOrder> CreateOrderAsync(long amountPaise, string receipt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.KeyId) || string.IsNullOrWhiteSpace(_options.KeySecret))
        {
            logger.LogError("Razorpay is not configured (missing key id or key secret) — cannot create an order.");
            throw new PaymentGatewayException();
        }

        if (amountPaise < 100)
        {
            // Razorpay's own floor for INR orders.
            throw new ValidationAppException(["Amount must be at least ₹1 (100 paise)."]);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, OrdersUrl)
        {
            Content = JsonContent.Create(new { amount = amountPaise, currency = "INR", receipt }, options: JsonOptions)
        };
        request.Headers.Authorization = BasicAuthHeader();

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Razorpay create-order request timed out.");
            throw new PaymentGatewayException();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Razorpay create-order request failed.");
            throw new PaymentGatewayException();
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            logger.LogError("Razorpay rejected our API credentials while creating an order.");
            throw new PaymentGatewayAuthException();
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Razorpay create-order failed with HTTP {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new PaymentGatewayException();
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            return new RazorpayOrder(
                root.GetProperty("id").GetString()!,
                root.GetProperty("amount").GetInt64(),
                root.GetProperty("currency").GetString()!);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogError(ex, "Razorpay create-order returned an unexpected response body: {Body}", body);
            throw new PaymentGatewayException();
        }
    }

    public bool VerifySignature(string orderId, string paymentId, string signature)
    {
        if (string.IsNullOrWhiteSpace(_options.KeySecret) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var payload = $"{orderId}|{paymentId}";
        var expected = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(_options.KeySecret), Encoding.UTF8.GetBytes(payload)));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
    }

    public async Task RefundAsync(string paymentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.KeyId) || string.IsNullOrWhiteSpace(_options.KeySecret))
        {
            logger.LogError("Razorpay is not configured (missing key id or key secret) — cannot refund.");
            throw new PaymentGatewayException();
        }

        // No "amount" field = refund the full amount originally captured for this payment.
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.razorpay.com/v1/payments/{Uri.EscapeDataString(paymentId)}/refund")
        {
            Content = JsonContent.Create(new { }, options: JsonOptions)
        };
        request.Headers.Authorization = BasicAuthHeader();

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Razorpay refund request timed out for payment {PaymentId}.", paymentId);
            throw new PaymentGatewayException("Unable to process the refund right now. Please try again.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Razorpay refund request failed for payment {PaymentId}.", paymentId);
            throw new PaymentGatewayException("Unable to process the refund right now. Please try again.");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            logger.LogError("Razorpay rejected our API credentials while refunding payment {PaymentId}.", paymentId);
            throw new PaymentGatewayAuthException();
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Razorpay refund failed with HTTP {StatusCode} for payment {PaymentId}: {Body}", (int)response.StatusCode, paymentId, body);
            throw new PaymentGatewayException("Unable to process the refund right now. Please try again.");
        }

        logger.LogInformation("Refunded Razorpay payment {PaymentId}.", paymentId);
    }

    private AuthenticationHeaderValue BasicAuthHeader() =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.KeyId}:{_options.KeySecret}")));
}
