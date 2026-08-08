using System.ComponentModel.DataAnnotations;

namespace TakeAuction.Api.Common.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = null!;

    [Required]
    public string Audience { get; init; } = null!;

    [Required]
    [MinLength(32)]
    public string SigningKey { get; init; } = null!;

    [Range(1, 1440)]
    public int AccessTokenLifetimeMinutes { get; init; } = 15;

    [Range(1, 90)]
    public int RefreshTokenLifetimeDays { get; init; } = 7;
}
