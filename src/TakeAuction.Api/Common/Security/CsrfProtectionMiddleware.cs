using System.Security.Cryptography;
using System.Text;
using Microsoft.Net.Http.Headers;

namespace TakeAuction.Api.Common.Security;

public sealed class CsrfProtectionMiddleware
{
    private static readonly string[] SafeMethods = ["GET", "HEAD", "OPTIONS", "TRACE"];

    private readonly RequestDelegate _next;
    private readonly ILogger<CsrfProtectionMiddleware> _logger;

    public CsrfProtectionMiddleware(RequestDelegate next, ILogger<CsrfProtectionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresValidation(context) && !IsValid(context))
        {
            _logger.LogWarning(
                "Rejected {Method} {Path} because the CSRF double-submit token was missing or mismatched",
                context.Request.Method,
                context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;

            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                title = "CSRF token missing or invalid",
                status = StatusCodes.Status403Forbidden,
                detail = $"Cookie-authenticated requests must send the '{AuthCookieWriter.CsrfHeaderName}' header "
                    + $"matching the '{AuthCookieWriter.CsrfCookieName}' cookie."
            });

            return;
        }

        await _next(context);
    }

    private static bool RequiresValidation(HttpContext context)
    {
        if (SafeMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (context.Request.Headers.ContainsKey(HeaderNames.Authorization))
        {
            return false;
        }

        return context.Request.Cookies.ContainsKey(AuthenticationExtensions.AccessTokenCookieName);
    }

    private static bool IsValid(HttpContext context)
    {
        var cookieToken = context.Request.Cookies[AuthCookieWriter.CsrfCookieName];
        var headerToken = context.Request.Headers[AuthCookieWriter.CsrfHeaderName].ToString();

        return !string.IsNullOrWhiteSpace(cookieToken)
            && !string.IsNullOrWhiteSpace(headerToken)
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(cookieToken),
                Encoding.UTF8.GetBytes(headerToken));
    }
}

public static class CsrfProtectionExtensions
{
    public static IApplicationBuilder UseTakeAuctionCsrfProtection(this IApplicationBuilder app) =>
        app.UseMiddleware<CsrfProtectionMiddleware>();
}
