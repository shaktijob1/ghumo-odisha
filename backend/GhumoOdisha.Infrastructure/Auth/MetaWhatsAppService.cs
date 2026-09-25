using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using GhumoOdisha.Application.Auth;
using GhumoOdisha.Application.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Infrastructure.Auth;

/// <summary>
/// Meta WhatsApp Cloud API: POST {ApiBaseUrl}/{ApiVersion}/{PhoneNumberId}/messages with a template
/// payload. A 200 carrying a message id (wamid) only means Meta accepted the send — delivery
/// failures (recipient not on WhatsApp, blocked, etc.) arrive later via webhooks. The request
/// body carries the OTP, so neither it nor the access token is ever logged.
/// </summary>
public class MetaWhatsAppService(HttpClient httpClient, IOptions<WhatsAppOptions> options, ILogger<MetaWhatsAppService> logger)
    : IWhatsAppService
{
    private readonly WhatsAppOptions _options = options.Value;

    public Task SendOtpAsync(string phoneNumber, string customerName, string otp, CancellationToken cancellationToken = default)
    {
        // Authentication templates take the code as the only body variable, and again as the
        // copy-code button's parameter.
        var components = _options.OtpTemplateCategory == WhatsAppOtpTemplateCategory.Authentication
            ? new object[] { Body([otp]), CopyCodeButton(otp) }
            : [Body([customerName, otp])];

        return SendAsync(phoneNumber, _options.OtpTemplateName, _options.OtpTemplateLanguage, components, "OTP", cancellationToken);
    }

    public Task SendTemplateAsync(string phoneNumber, string variable1, string variable2, CancellationToken cancellationToken = default)
    {
        if (_options.OtpTemplateCategory == WhatsAppOtpTemplateCategory.Authentication)
        {
            logger.LogWarning("WhatsApp OTP template is an Authentication template and can't carry notices — skipping message to {PhoneLast4}.",
                LastFour(phoneNumber));
            throw new WhatsAppDeliveryException();
        }

        return SendAsync(phoneNumber, _options.OtpTemplateName, _options.OtpTemplateLanguage,
            [Body([variable1, variable2])], "template message", cancellationToken);
    }

    public async Task<bool> SendBookingConfirmedAsync(string phoneNumber, BookingConfirmedWhatsAppMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BookingConfirmedTemplateName))
        {
            return false;
        }

        await SendAsync(phoneNumber, _options.BookingConfirmedTemplateName, _options.BookingConfirmedTemplateLanguage,
            [Body(message.ToTemplateVariables())], "booking-confirmed message", cancellationToken);
        return true;
    }

    private async Task SendAsync(string phoneNumber, string templateName, string language, object[] components, string kind, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken) || string.IsNullOrWhiteSpace(_options.PhoneNumberId) ||
            string.IsNullOrWhiteSpace(templateName))
        {
            logger.LogError("WhatsApp is not configured (missing access token, phone number id or template name) — cannot send {Kind}.", kind);
            throw new WhatsAppDeliveryException();
        }

        var url = $"{_options.ApiBaseUrl.TrimEnd('/')}/{_options.ApiVersion}/{Uri.EscapeDataString(_options.PhoneNumberId)}/messages";
        var payload = new
        {
            messaging_product = "whatsapp",
            to = ToInternationalNumber(phoneNumber),
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = language },
                components
            }
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("WhatsApp {Kind} to {PhoneLast4} rejected by Meta with HTTP {StatusCode}: {Error}",
                    kind, LastFour(phoneNumber), (int)response.StatusCode, DescribeError(body));
                throw new WhatsAppDeliveryException();
            }

            logger.LogInformation("WhatsApp {Kind} accepted by Meta for {PhoneLast4} (message id {MessageId}).",
                kind, LastFour(phoneNumber), ReadMessageId(body));
        }
        catch (WhatsAppDeliveryException)
        {
            throw;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "WhatsApp Cloud API request timed out.");
            throw new WhatsAppDeliveryException();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "WhatsApp Cloud API request failed.");
            throw new WhatsAppDeliveryException();
        }
    }

    private static object Body(IEnumerable<string> values) => new
    {
        type = "body",
        parameters = values.Select(v => new { type = "text", text = SanitizeParameter(v) }).ToArray()
    };

    private static object CopyCodeButton(string otp) => new
    {
        type = "button",
        sub_type = "url",
        index = "0",
        parameters = new[] { new { type = "text", text = otp } }
    };

    // WhatsApp rejects template parameters that are empty or contain newlines/tabs or long runs of
    // spaces — so a trip title or pickup location typed with any of those would fail the whole send.
    private static string SanitizeParameter(string value)
    {
        var cleaned = Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
        return cleaned.Length == 0 ? "-" : cleaned;
    }

    // Meta needs the full international number without "+". Customer numbers are stored as bare
    // 10-digit Indian mobiles; the organizer's is configured with the country code already.
    private static string ToInternationalNumber(string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        return digits.Length == 10 ? "91" + digits : digits;
    }

    // Meta's error envelope: {"error":{"message","code","error_subcode","error_data":{"details"},"fbtrace_id"}}.
    // Only these fields are logged — never the raw request.
    private static string DescribeError(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                var code = error.TryGetProperty("code", out var c) ? c.ToString() : "?";
                var message = error.TryGetProperty("message", out var m) ? m.GetString() : null;
                var details = error.TryGetProperty("error_data", out var d) && d.TryGetProperty("details", out var dd) ? dd.GetString() : null;
                var trace = error.TryGetProperty("fbtrace_id", out var t) ? t.GetString() : null;
                return $"#{code} {message}{(details is null ? "" : $" — {details}")} (fbtrace_id {trace})";
            }
        }
        catch (JsonException)
        {
        }

        return "unrecognised response";
    }

    private static string ReadMessageId(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.TryGetProperty("messages", out var messages) &&
                   messages.ValueKind == JsonValueKind.Array && messages.GetArrayLength() > 0 &&
                   messages[0].TryGetProperty("id", out var id)
                ? id.GetString() ?? "none"
                : "none";
        }
        catch (JsonException)
        {
            return "none";
        }
    }

    private static string LastFour(string phoneNumber) =>
        phoneNumber.Length <= 4 ? phoneNumber : "…" + phoneNumber[^4..];
}
