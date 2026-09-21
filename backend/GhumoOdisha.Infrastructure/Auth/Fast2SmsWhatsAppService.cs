using System.Text.Json;
using GhumoOdisha.Application.Auth;
using GhumoOdisha.Application.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Infrastructure.Auth;

/// <summary>
/// Fast2SMS's WhatsApp trigger API: GET with authorization/message_id/numbers/variables_values
/// as query parameters. Fast2SMS (like their SMS API) can return HTTP 200 with a JSON body
/// reporting a logical failure (e.g. {"return":false,"message":"..."}), so both the transport
/// status and the response body are checked before treating the send as successful.
/// </summary>
public class Fast2SmsWhatsAppService(HttpClient httpClient, IOptions<Fast2SmsOptions> options, ILogger<Fast2SmsWhatsAppService> logger)
    : IFast2SmsWhatsAppService
{
    private readonly Fast2SmsOptions _options = options.Value;

    public Task SendOtpAsync(string phoneNumber, string customerName, string otp, CancellationToken cancellationToken = default) =>
        SendAsync(phoneNumber, customerName, otp, isOtp: true, cancellationToken);

    public Task SendTemplateAsync(string phoneNumber, string variable1, string variable2, CancellationToken cancellationToken = default) =>
        SendAsync(phoneNumber, variable1, variable2, isOtp: false, cancellationToken);

    private async Task SendAsync(string phoneNumber, string variable1, string variable2, bool isOtp, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.MessageId))
        {
            logger.LogError("Fast2SMS is not configured (missing API key or message id) — cannot send WhatsApp message.");
            throw new WhatsAppDeliveryException();
        }

        var variables = string.Join('|', variable1, variable2);
        var query = $"?authorization={Uri.EscapeDataString(_options.ApiKey)}" +
                    $"&message_id={Uri.EscapeDataString(_options.MessageId)}" +
                    $"&numbers={Uri.EscapeDataString(phoneNumber)}" +
                    $"&variables_values={Uri.EscapeDataString(variables)}";

        try
        {
            using var response = await httpClient.GetAsync(_options.BaseUrl + query, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Fast2SMS WhatsApp send failed with HTTP {StatusCode} for a request to {PhoneLast4}.",
                    (int)response.StatusCode, LastFour(phoneNumber));
                throw new WhatsAppDeliveryException();
            }

            if (!IndicatesSuccess(body))
            {
                logger.LogError("Fast2SMS WhatsApp send reported failure for a request to {PhoneLast4}: {ResponseBody}",
                    LastFour(phoneNumber), body);
                throw new WhatsAppDeliveryException();
            }

            logger.LogInformation("WhatsApp {Kind} dispatched via Fast2SMS to {PhoneLast4}.", isOtp ? "OTP" : "template message", LastFour(phoneNumber));
        }
        catch (WhatsAppDeliveryException)
        {
            throw;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Fast2SMS WhatsApp request timed out.");
            throw new WhatsAppDeliveryException();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Fast2SMS WhatsApp request failed.");
            throw new WhatsAppDeliveryException();
        }
    }

    private static bool IndicatesSuccess(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("return", out var returnProp))
            {
                return returnProp.ValueKind switch
                {
                    JsonValueKind.False => false,
                    JsonValueKind.True => true,
                    _ => true
                };
            }
        }
        catch (JsonException)
        {
            // Non-JSON response body (some Fast2SMS endpoints reply with plain text on success) — treat as success
            // since the HTTP status was already confirmed OK above.
        }

        return true;
    }

    private static string LastFour(string phoneNumber) =>
        phoneNumber.Length <= 4 ? phoneNumber : "…" + phoneNumber[^4..];
}
