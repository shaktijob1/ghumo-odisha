using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Company;
using GhumoOdisha.Application.Homepage;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Application.Contact;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/contact")]
public class ContactController(
    IOptions<OrganizerContactOptions> options,
    IOptions<CompanyOptions> companyOptions,
    IOrganizerProfileService organizerProfileService,
    ISiteHeroPhotoService sitePhotoService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<ContactDto>>> GetContact(CancellationToken cancellationToken)
    {
        // Texts the admin set on the Travel Organizer card, else appsettings OrganizerContact. The
        // WhatsApp number always comes from appsettings (it's tied to the WhatsApp Business templates).
        var card = await organizerProfileService.GetCardAsync(cancellationToken);
        var company = companyOptions.Value;
        var officePhotoUrl = await sitePhotoService.GetPhotoUrlAsync(SiteHeroPage.Office, cancellationToken);
        var dto = new ContactDto(card.Name, card.Role, card.Phone, options.Value.WhatsAppNumber, card.Email, card.PhotoUrl,
            card.InstagramUrl, company.DisplayAddress(), company.DirectionsUrl(), officePhotoUrl,
            card.Quote, card.PhotoCaption, card.HelpTitle, card.HelpText);
        return Ok(ApiResponse<ContactDto>.Ok(dto));
    }
}
