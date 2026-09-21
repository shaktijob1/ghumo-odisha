namespace GhumoOdisha.Application.Customers.Dtos;

public record CustomerProfileDto(
    int CustomerId,
    string Name,
    string PhoneNumber,
    string? Email,
    bool IsVerified,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public record UpdateProfileRequest(string Name, string? Email);

public record AdminCustomerListItemDto(
    int CustomerId,
    string Name,
    string PhoneNumber,
    string? Email,
    int TripCount,
    int ConfirmedBookingCount,
    decimal TotalAmount,
    decimal AdvancePaid,
    decimal Remaining);

public record AdminCustomerDetailDto(
    int CustomerId,
    string Name,
    string PhoneNumber,
    string? Email,
    bool IsVerified,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    int TotalBookings,
    int ConfirmedBookingCount,
    decimal TotalAmount,
    decimal AdvancePaid,
    decimal Remaining);
