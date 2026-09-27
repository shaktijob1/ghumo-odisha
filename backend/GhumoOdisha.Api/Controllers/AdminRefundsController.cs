using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Refunds;
using GhumoOdisha.Application.Refunds.Dtos;
using GhumoOdisha.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/admin/refunds")]
[Authorize(Roles = "Admin")]
public class AdminRefundsController(IRefundService refundService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminRefundDto>>>> GetRefunds(
        [FromQuery] RefundStatus? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await refundService.GetRefundsAsync(status, search, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminRefundDto>>.Ok(result));
    }

    [HttpGet("counts")]
    public async Task<ActionResult<ApiResponse<RefundCountsDto>>> GetCounts(CancellationToken cancellationToken)
    {
        var result = await refundService.GetCountsAsync(cancellationToken);
        return Ok(ApiResponse<RefundCountsDto>.Ok(result));
    }

    [HttpPost("{id:int}/razorpay")]
    public async Task<ActionResult<ApiResponse<AdminRefundDto>>> IssueRazorpay(int id, IssueRazorpayRefundRequest request, CancellationToken cancellationToken)
    {
        var result = await refundService.IssueRazorpayRefundAsync(id, request, cancellationToken);
        return Ok(ApiResponse<AdminRefundDto>.Ok(result, "Razorpay refund issued."));
    }

    [HttpPost("{id:int}/manual")]
    public async Task<ActionResult<ApiResponse<AdminRefundDto>>> RecordManual(int id, RecordManualRefundRequest request, CancellationToken cancellationToken)
    {
        var result = await refundService.RecordManualRefundAsync(id, request, cancellationToken);
        return Ok(ApiResponse<AdminRefundDto>.Ok(result, "Manual refund recorded."));
    }

    [HttpPost("{id:int}/settle")]
    public async Task<ActionResult<ApiResponse<AdminRefundDto>>> Settle(int id, SettleRefundRequest request, CancellationToken cancellationToken)
    {
        var result = await refundService.SettleAsync(id, request, cancellationToken);
        return Ok(ApiResponse<AdminRefundDto>.Ok(result, "Refund marked as settled."));
    }

    [HttpGet("{id:int}/gateway-status")]
    public async Task<ActionResult<ApiResponse<object>>> GatewayStatus(int id, CancellationToken cancellationToken)
    {
        var status = await refundService.GetGatewayStatusAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { status }));
    }
}
