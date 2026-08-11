using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace TakeAuction.Api.Common.Security;

public sealed class AuthCookieWriter
{
    public const string CsrfCookieName = "takeauction_csrf";
    public const string CsrfHeaderName = "X-CSRF-TOKEN";

    private readonly AuthCookieOptions _options;

    public AuthCookieWriter(IOptions<AuthCookieOptions> options) => _options = options.Value;

    public void Write(HttpContext context, AccessToken accessToken)
    {
        var sameSite = Enum.TryParse<SameSiteMode>(_options.SameSite, ignoreCase: true, out var parsed)
            ? parsed
            : SameSiteMode.Lax;

        var secure = _options.SecureAlways
            || context.Request.IsHttps
            || sameSite == SameSiteMode.None;

        context.Response.Cookies.Append(
            AuthenticationExtensions.AccessTokenCookieName,
            accessToken.Value,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = sameSite,
                Domain = _options.Domain,
                Path = "/",
                Expires = accessToken.ExpiresAtUtc,
                IsEssential = true
            });

        context.Response.Cookies.Append(
            CsrfCookieName,
            RandomNumberGenerator.GetHexString(48, lowercase: true),
            new CookieOptions
            {
                HttpOnly = false,
                Secure = secure,
                SameSite = sameSite,
                Domain = _options.Domain,
                Path = "/",
                Expires = accessToken.ExpiresAtUtc,
                IsEssential = true
            });
    }

    public void Clear(HttpContext context)
    {
        var sameSite = Enum.TryParse<SameSiteMode>(_options.SameSite, ignoreCase: true, out var parsed)
            ? parsed
            : SameSiteMode.Lax;

        var secure = _options.SecureAlways
            || context.Request.IsHttps
            || sameSite == SameSiteMode.None;

        var expired = new CookieOptions
        {
            Secure = secure,
            SameSite = sameSite,
            Domain = _options.Domain,
            Path = "/"
        };

        context.Response.Cookies.Delete(AuthenticationExtensions.AccessTokenCookieName, expired);
        context.Response.Cookies.Delete(CsrfCookieName, expired);
    }
}
