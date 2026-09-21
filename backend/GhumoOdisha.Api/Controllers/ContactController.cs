using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Contact;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/contact")]
public class ContactController(IOptions<OrganizerContactOptions> options, IOrganizerProfileService organizerProfileService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<ContactDto>>> GetContact(CancellationToken cancellationToken)
    {
        var contact = options.Value;
        var photoUrl = await organizerProfileService.GetPhotoUrlAsync(cancellationToken);
        var dto = new ContactDto(contact.Name, contact.Role, contact.Phone, contact.WhatsAppNumber, contact.Email, photoUrl);
        return Ok(ApiResponse<ContactDto>.Ok(dto));
    }
}
