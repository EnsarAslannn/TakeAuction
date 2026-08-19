using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TakeAuction.Api.Common.Api;

public static class SwaggerExtensions
{
    public const string BearerSchemeId = "Bearer";

    public static IServiceCollection AddTakeAuctionSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.ConfigureOptions<ConfigureSwaggerOptions>();

        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition(BearerSchemeId, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "JWT Bearer token. Paste only the token; the 'Bearer ' prefix is added automatically.",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerSchemeId, document)] = []
            });
        });

        return services;
    }

    public static WebApplication UseTakeAuctionSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            foreach (var description in app.DescribeApiVersions().Reverse())
            {
                options.SwaggerEndpoint(
                    $"/swagger/{description.GroupName}/swagger.json",
                    $"TakeAuction API {description.GroupName.ToUpperInvariant()}");
            }

            options.DocumentTitle = "TakeAuction API";

            options.UseRequestInterceptor(
                "(request) => { const m = document.cookie.match(/(?:^|; )takeauction_csrf=([^;]*)/);"
                + " if (m) { request.headers['X-CSRF-TOKEN'] = decodeURIComponent(m[1]); } return request; }");
        });

        return app;
    }
}

public sealed class ConfigureSwaggerOptions : IConfigureNamedOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider) => _provider = provider;

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, CreateVersionInfo(description));
        }
    }

    public void Configure(string? name, SwaggerGenOptions options) => Configure(options);

    private static OpenApiInfo CreateVersionInfo(ApiVersionDescription description)
    {
        var info = new OpenApiInfo
        {
            Title = "TakeAuction API",
            Version = description.ApiVersion.ToString(),
            Description = "Live auction platform API."
        };

        if (description.IsDeprecated)
        {
            info.Description += " This API version has been deprecated.";
        }

        return info;
    }
}
