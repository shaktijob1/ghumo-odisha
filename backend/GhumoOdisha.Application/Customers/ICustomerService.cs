using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Customers.Dtos;

namespace GhumoOdisha.Application.Customers;

public interface ICustomerService
{
    Task<CustomerProfileDto> GetProfileAsync(int customerId, CancellationToken cancellationToken = default);

    Task UpdateProfileAsync(int customerId, UpdateProfileRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<AdminCustomerListItemDto>> GetAdminCustomersAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<AdminCustomerDetailDto> GetAdminCustomerDetailAsync(int customerId, CancellationToken cancellationToken = default);
}
