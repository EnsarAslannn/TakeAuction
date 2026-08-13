using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace TakeAuction.Api.Common.Security;

public static class AuthenticationExtensions
{
    public const string AccessTokenCookieName = "takeauction_access_token";

    public static IServiceCollection AddTakeAuctionAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AuthCookieOptions>()
            .Bind(configuration.GetSection(AuthCookieOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
        services.AddScoped<ISessionIssuer, SessionIssuer>();
        services.AddSingleton<AuthCookieWriter>();

        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var issuer = jwtSection[nameof(JwtOptions.Issuer)];
        var audience = jwtSection[nameof(JwtOptions.Audience)];
        var signingKey = jwtSection[nameof(JwtOptions.SigningKey)];

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(signingKey ?? string.Empty)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrEmpty(context.Token))
                        {
                            context.Token = context.Request.Cookies[AccessTokenCookieName];
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }
}
