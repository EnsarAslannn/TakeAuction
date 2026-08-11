namespace TakeAuction.Api.Common.Api;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; init; } = [];
}

public static class CorsExtensions
{
    public const string PolicyName = "takeauction-web";

    public static IServiceCollection AddTakeAuctionCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

        services.AddCors(cors => cors.AddPolicy(PolicyName, policy =>
        {
            if (options.AllowedOrigins.Length == 0)
            {
                return;
            }

            policy.WithOrigins(options.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithExposedHeaders("Retry-After");
        }));

        return services;
    }
}
