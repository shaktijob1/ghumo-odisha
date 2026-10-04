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
        var contact = options.Value;
        var photoUrl = await organizerProfileService.GetPhotoUrlAsync(cancellationToken);
        var company = companyOptions.Value;
        var officePhotoUrl = await sitePhotoService.GetPhotoUrlAsync(SiteHeroPage.Office, cancellationToken);
        var dto = new ContactDto(contact.Name, contact.Role, contact.Phone, contact.WhatsAppNumber, contact.Email, photoUrl,
            string.IsNullOrWhiteSpace(contact.InstagramUrl) ? null : contact.InstagramUrl,
            company.DisplayAddress(), company.DirectionsUrl(), officePhotoUrl);
        return Ok(ApiResponse<ContactDto>.Ok(dto));
    }
}
