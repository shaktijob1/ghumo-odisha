namespace GhumoOdisha.Application.Auth;

/// <summary>
/// Google sign-in settings. Only the OAuth web client id is needed: the browser gets an ID token
/// from Google Identity Services and the API verifies it against this id. The client secret is
/// not used by this flow — don't put it in config.
/// </summary>
public class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    public string ClientId { get; set; } = string.Empty;
}
