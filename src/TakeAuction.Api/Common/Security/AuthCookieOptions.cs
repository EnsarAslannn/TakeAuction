namespace TakeAuction.Api.Common.Security;

public sealed class AuthCookieOptions
{
    public const string SectionName = "AuthCookies";

    public string? Domain { get; init; }

    public bool SecureAlways { get; init; }

    public string SameSite { get; init; } = nameof(Microsoft.AspNetCore.Http.SameSiteMode.Lax);
}
