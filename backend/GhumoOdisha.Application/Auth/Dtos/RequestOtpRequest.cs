namespace GhumoOdisha.Application.Auth.Dtos;

public record RequestOtpRequest(string? Name, string WhatsAppNumber);

public record RequestOtpResponse(int OtpLength);
