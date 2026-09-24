using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Legal;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/terms")]
public class TermsController : ControllerBase
{
    [HttpGet]
    public ActionResult<ApiResponse<object>> GetTerms()
    {
        return Ok(ApiResponse<object>.Ok(new { version = TermsAndConditionsContent.Version, text = TermsAndConditionsContent.Text }));
    }
}
