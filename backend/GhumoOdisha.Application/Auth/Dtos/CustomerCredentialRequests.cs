namespace GhumoOdisha.Application.Auth.Dtos;

/// <summary>The ID token ("credential") Google Identity Services hands the browser.</summary>
public record GoogleSignInRequest(string Credential);

/// <summary>Adding a WhatsApp number to a signed-in account (step 1: send OTP).</summary>
public record AddPhoneRequest(string WhatsAppNumber);
