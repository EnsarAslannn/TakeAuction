using Microsoft.Extensions.Options;

namespace TakeAuction.Api.Common.Security;

public sealed class SecurityHeaderOptions
{
    public const string SectionName = "SecurityHeaders";

    public string ContentSecurityPolicy { get; init; } =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    public string FrameOptions { get; init; } = "DENY";

    public string ReferrerPolicy { get; init; } = "no-referrer";

    public string CrossOriginOpenerPolicy { get; init; } = "same-origin";

    public string CrossOriginResourcePolicy { get; init; } = "cross-origin";

    public string[] PolicyExemptPathPrefixes { get; init; } = ["/swagger", "/hangfire"];
}

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityHeaderOptions _options;

    public SecurityHeadersMiddleware(RequestDelegate next, IOptions<SecurityHeaderOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var (httpContext, options) = ((HttpContext, SecurityHeaderOptions))state;

            Apply(httpContext, options);

            return Task.CompletedTask;
        }, (context, _options));

        return _next(context);
    }

    private static void Apply(HttpContext context, SecurityHeaderOptions options)
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";

        Set(headers, "Referrer-Policy", options.ReferrerPolicy);
        Set(headers, "Cross-Origin-Opener-Policy", options.CrossOriginOpenerPolicy);
        Set(headers, "Cross-Origin-Resource-Policy", options.CrossOriginResourcePolicy);

        if (IsPolicyExempt(context, options))
        {
            return;
        }

        Set(headers, "X-Frame-Options", options.FrameOptions);
        Set(headers, "Content-Security-Policy", options.ContentSecurityPolicy);
    }

    private static void Set(IHeaderDictionary headers, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            headers[name] = value;
        }
    }

    private static bool IsPolicyExempt(HttpContext context, SecurityHeaderOptions options) =>
        options.PolicyExemptPathPrefixes.Any(prefix =>
            context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
}

public static class SecurityHeadersExtensions
{
    public static IServiceCollection AddTakeAuctionSecurityHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<SecurityHeaderOptions>()
            .Bind(configuration.GetSection(SecurityHeaderOptions.SectionName));

        return services;
    }

    public static IApplicationBuilder UseTakeAuctionSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}
