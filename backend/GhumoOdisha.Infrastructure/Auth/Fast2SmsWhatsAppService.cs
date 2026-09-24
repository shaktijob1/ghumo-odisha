using System.Text.Json;
using System.Text.RegularExpressions;
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
        SendAsync(phoneNumber, _options.MessageId, [AfterGreeting(customerName), otp], "OTP", cancellationToken);

    public Task SendTemplateAsync(string phoneNumber, string variable1, string variable2, CancellationToken cancellationToken = default) =>
        SendAsync(phoneNumber, _options.MessageId, [AfterGreeting(variable1), variable2], "template message", cancellationToken);

    // The shared MessageId template was approved as "Hello{{1}}" with no space before the variable,
    // so the space has to travel inside the value or the message reads "Hellothere" / "HelloRahul".
    private static string AfterGreeting(string value) => " " + value.Trim();

    public async Task<bool> SendBookingConfirmedAsync(string phoneNumber, BookingConfirmedWhatsAppMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BookingConfirmedMessageId))
        {
            return false;
        }

        await SendAsync(phoneNumber, _options.BookingConfirmedMessageId, message.ToTemplateVariables().Select(v => v.Trim()).ToList(), "booking-confirmed message", cancellationToken);
        return true;
    }

    private async Task SendAsync(string phoneNumber, string messageId, IReadOnlyList<string> variableValues, string kind, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(messageId))
        {
            logger.LogError("Fast2SMS is not configured (missing API key or message id) — cannot send WhatsApp {Kind}.", kind);
            throw new WhatsAppDeliveryException();
        }

        var variables = string.Join('|', variableValues.Select(SanitizeVariable));
        var query = $"?authorization={Uri.EscapeDataString(_options.ApiKey)}" +
                    $"&message_id={Uri.EscapeDataString(messageId)}" +
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

            logger.LogInformation("WhatsApp {Kind} dispatched via Fast2SMS to {PhoneLast4}.", kind, LastFour(phoneNumber));
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

    // "|" is Fast2SMS's variable separator, and WhatsApp rejects template parameters containing
    // newlines/tabs or runs of spaces — so a trip title or pickup location typed with any of those
    // would otherwise shift every following variable or fail the whole send. Whitespace runs are
    // collapsed to one space rather than trimmed, so AfterGreeting's leading space survives.
    private static string SanitizeVariable(string value) =>
        Regex.Replace(value.Replace('|', '/'), @"\s+", " ");

    private static string LastFour(string phoneNumber) =>
        phoneNumber.Length <= 4 ? phoneNumber : "…" + phoneNumber[^4..];
}
