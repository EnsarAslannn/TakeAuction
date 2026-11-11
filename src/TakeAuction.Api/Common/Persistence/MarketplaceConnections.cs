using Npgsql;
using StackExchange.Redis;

namespace TakeAuction.Api.Common.Persistence;

internal static class MarketplaceConnections
{
    public static void Apply(IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Postgres")) &&
            configuration["PGHOST_UNPOOLED"] is { Length: > 0 } host)
        {
            var connection = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Database = Required(configuration, "PGDATABASE"),
                Username = Required(configuration, "PGUSER"),
                Password = Required(configuration, "PGPASSWORD"),
                SslMode = SslMode.VerifyFull,
                MaxPoolSize = 12
            };
            configuration["ConnectionStrings:Postgres"] = connection.ConnectionString;
        }

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Redis")) &&
            configuration["REDIS_URL"] is { Length: > 0 } redisUrl)
        {
            configuration["ConnectionStrings:Redis"] = RedisOptions(redisUrl).ToString(includePassword: true);
        }
    }

    private static ConfigurationOptions RedisOptions(string redisUrl)
    {
        if (!Uri.TryCreate(redisUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != "rediss" || string.IsNullOrEmpty(uri.Host))
        {
            throw new InvalidOperationException("REDIS_URL must be an authenticated TLS Redis URL.");
        }

        var credentials = uri.UserInfo.Split(':', 2);
        if (credentials.Length != 2 || string.IsNullOrEmpty(credentials[1]))
        {
            throw new InvalidOperationException("REDIS_URL must include a password.");
        }

        var options = new ConfigurationOptions
        {
            Ssl = true,
            AbortOnConnectFail = false,
            User = string.IsNullOrEmpty(credentials[0]) ? null : Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1])
        };
        options.EndPoints.Add(uri.IdnHost, uri.Port < 0 ? 6379 : uri.Port);
        return options;
    }

    private static string Required(IConfiguration configuration, string name) =>
        configuration[name] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{name} is required for the Neon PostgreSQL connection.");
}
