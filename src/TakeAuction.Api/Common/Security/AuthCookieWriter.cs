using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace TakeAuction.Api.Common.Security;

public sealed class AuthCookieWriter
{
    public const string CsrfCookieName = "takeauction_csrf";
    public const string CsrfHeaderName = "X-CSRF-TOKEN";
    public const string RefreshCookieName = "takeauction_refresh_token";

    /// <summary>
    /// Scoped to the auth slice rather than the whole site, so the long-lived credential stays
    /// off the wire on the hot bidding path and only travels to refresh and logout.
    /// </summary>
    public const string RefreshCookiePath = "/api/v1/auth";

    private readonly AuthCookieOptions _options;

    public AuthCookieWriter(IOptions<AuthCookieOptions> options) => _options = options.Value;

    public void Write(HttpContext context, IssuedSession session)
    {
        Write(context, session.AccessToken);

        context.Response.Cookies.Append(
            RefreshCookieName,
            session.RefreshToken.Value,
            BuildOptions(context, httpOnly: true, session.RefreshToken.ExpiresAtUtc, RefreshCookiePath));
    }

    public void Write(HttpContext context, AccessToken accessToken)
    {
        context.Response.Cookies.Append(
            AuthenticationExtensions.AccessTokenCookieName,
            accessToken.Value,
            BuildOptions(context, httpOnly: true, accessToken.ExpiresAtUtc, "/"));

        context.Response.Cookies.Append(
            CsrfCookieName,
            RandomNumberGenerator.GetHexString(48, lowercase: true),
            BuildOptions(context, httpOnly: false, accessToken.ExpiresAtUtc, "/"));
    }

    public static string? ReadRefreshToken(HttpContext context) =>
        context.Request.Cookies[RefreshCookieName];

    public void Clear(HttpContext context)
    {
        context.Response.Cookies.Delete(
            AuthenticationExtensions.AccessTokenCookieName,
            BuildOptions(context, httpOnly: true, expiresAt: null, "/"));

        context.Response.Cookies.Delete(
            CsrfCookieName,
            BuildOptions(context, httpOnly: false, expiresAt: null, "/"));

        // Deleting a cookie only works when the path matches the one it was written with.
        context.Response.Cookies.Delete(
            RefreshCookieName,
            BuildOptions(context, httpOnly: true, expiresAt: null, RefreshCookiePath));
    }

    private CookieOptions BuildOptions(
        HttpContext context,
        bool httpOnly,
        DateTimeOffset? expiresAt,
        string path)
    {
        var sameSite = Enum.TryParse<SameSiteMode>(_options.SameSite, ignoreCase: true, out var parsed)
            ? parsed
            : SameSiteMode.Lax;

        var secure = _options.SecureAlways
            || context.Request.IsHttps
            || sameSite == SameSiteMode.None;

        return new CookieOptions
        {
            HttpOnly = httpOnly,
            Secure = secure,
            SameSite = sameSite,
            Domain = _options.Domain,
            Path = path,
            Expires = expiresAt,
            IsEssential = true
        };
    }
}
