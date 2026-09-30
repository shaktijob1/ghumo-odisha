using System.Net;
using System.Text.Json;
using GhumoOdisha.Application.Auth;
using GhumoOdisha.Infrastructure.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Tests;

/// <summary>Checks the exact request sent to Meta for the single-variable "pathauchi" template — nothing is sent.</summary>
public class MetaWhatsAppPayloadTests
{
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public JsonDocument? LastBody { get; private set; }
        public Uri? LastUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            LastBody = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"messages\":[{\"id\":\"wamid.test\"}]}") };
        }
    }

    private static (MetaWhatsAppService Service, CaptureHandler Handler) Create(WhatsAppOtpTemplateCategory category)
    {
        var handler = new CaptureHandler();
        var options = Options.Create(new WhatsAppOptions
        {
            PhoneNumberId = "123",
            AccessToken = "test-token",
            OtpTemplateName = "pathauchi",
            OtpTemplateLanguage = "en",
            OtpTemplateCategory = category
        });
        return (new MetaWhatsAppService(new HttpClient(handler), options, NullLogger<MetaWhatsAppService>.Instance), handler);
    }

    private static string[] BodyParameters(JsonDocument doc) =>
        doc.RootElement.GetProperty("template").GetProperty("components")[0].GetProperty("parameters")
            .EnumerateArray().Select(p => p.GetProperty("text").GetString()!).ToArray();

    [Fact]
    public async Task SingleVariable_Otp_SendsOneParameterWithTheCode()
    {
        var (service, handler) = Create(WhatsAppOtpTemplateCategory.SingleVariable);

        await service.SendOtpAsync("9876543210", "Priya", "482913");

        var template = handler.LastBody!.RootElement.GetProperty("template");
        Assert.Equal("pathauchi", template.GetProperty("name").GetString());
        Assert.Equal("en", template.GetProperty("language").GetProperty("code").GetString());
        Assert.Equal("919876543210", handler.LastBody.RootElement.GetProperty("to").GetString());
        var parameter = Assert.Single(BodyParameters(handler.LastBody));
        Assert.Equal("Your Ghumo Odisha verification code is 482913. Please do not share it with anyone.", parameter);
    }

    [Fact]
    public async Task SingleVariable_BookingNotice_SendsOnlyTheMessage()
    {
        var (service, handler) = Create(WhatsAppOtpTemplateCategory.SingleVariable);

        await service.SendTemplateAsync("9876543210", "Priya", "Your booking GO-42 is confirmed: Puri • Konark, 03 Oct-04 Oct, 2 seat(s).");

        var parameter = Assert.Single(BodyParameters(handler.LastBody!));
        Assert.StartsWith("Your booking GO-42 is confirmed", parameter);
    }

    [Fact]
    public async Task Utility_KeepsTwoParameters()
    {
        var (service, handler) = Create(WhatsAppOtpTemplateCategory.Utility);

        await service.SendOtpAsync("9876543210", "Priya", "482913");

        Assert.Equal(["Priya", "OTP 482913"], BodyParameters(handler.LastBody!));
    }
}
