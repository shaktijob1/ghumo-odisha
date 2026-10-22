using GhumoOdisha.Api.Auth;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Crm;
using GhumoOdisha.Application.Crm.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

/// <summary>Admin → CRM → Customers. The same customers as the website, with CRM fields.</summary>
[ApiController]
[Route("api/admin/crm")]
[Authorize(Roles = "Admin")]
public class AdminCrmCustomersController(ICrmCustomerService customers) : ControllerBase
{
    [HttpGet("customers")]
    public async Task<ActionResult<ApiResponse<PagedResult<CrmCustomerListItemDto>>>> GetCustomers([FromQuery] CrmCustomerFilter filter, CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<CrmCustomerListItemDto>>.Ok(await customers.GetCustomersAsync(filter, cancellationToken)));

    [HttpGet("customers/{id:int}")]
    public async Task<ActionResult<ApiResponse<CrmCustomerDetailDto>>> GetCustomer(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CrmCustomerDetailDto>.Ok(await customers.GetCustomerAsync(id, cancellationToken)));

    /// <summary>Duplicate check before adding a customer: is this mobile already someone's (main or alternate)?</summary>
    [HttpGet("customers/check-phone")]
    public async Task<ActionResult<ApiResponse<CrmDuplicateCheckDto>>> CheckPhone([FromQuery] string phone, [FromQuery] int? exceptCustomerId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CrmDuplicateCheckDto>.Ok(await customers.CheckPhoneAsync(phone, exceptCustomerId, cancellationToken)));

    [HttpPost("customers")]
    public async Task<ActionResult<ApiResponse<object>>> CreateCustomer(CreateCrmCustomerRequest request, CancellationToken cancellationToken)
    {
        var customerId = await customers.CreateCustomerAsync(request, User.GetAdminId(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { customerId }, "Customer added."));
    }

    [HttpPut("customers/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateCustomer(int id, UpdateCrmCustomerRequest request, CancellationToken cancellationToken)
    {
        await customers.UpdateCustomerAsync(id, request, User.GetAdminId(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Customer updated."));
    }

    [HttpPut("customers/{id:int}/status")]
    public async Task<ActionResult<ApiResponse<object>>> ChangeStatus(int id, ChangeCrmStatusRequest request, CancellationToken cancellationToken)
    {
        await customers.ChangeStatusAsync(id, request, User.GetAdminId(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Status updated."));
    }

    [HttpPost("customers/{id:int}/notes")]
    public async Task<ActionResult<ApiResponse<CrmNoteDto>>> AddNote(int id, AddCrmNoteRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CrmNoteDto>.Ok(await customers.AddNoteAsync(id, request, User.GetAdminId(), cancellationToken), "Note added."));

    /// <summary>Admins a customer or follow-up can be assigned to.</summary>
    [HttpGet("admins")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CrmAdminUserDto>>>> GetAdmins(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<CrmAdminUserDto>>.Ok(await customers.GetAdminUsersAsync(cancellationToken)));
}
