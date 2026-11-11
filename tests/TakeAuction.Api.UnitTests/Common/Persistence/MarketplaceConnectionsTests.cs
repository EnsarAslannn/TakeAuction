using System.Net;
using Microsoft.Extensions.Configuration;
using Npgsql;
using StackExchange.Redis;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.UnitTests.Common.Persistence;

public sealed class MarketplaceConnectionsTests
{
    [Fact]
    public void UsesTheUnpooledNeonHostWithCertificateValidation()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["PGHOST"] = "pooled.example.com",
            ["PGHOST_UNPOOLED"] = "direct.example.com",
            ["PGDATABASE"] = "takeauction",
            ["PGUSER"] = "takeauction",
            ["PGPASSWORD"] = "test;password=quoted"
        });

        MarketplaceConnections.Apply(configuration);

        var connection = new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("Postgres"));
        Assert.Equal("direct.example.com", connection.Host);
        Assert.Equal("takeauction", connection.Database);
        Assert.Equal("test;password=quoted", connection.Password);
        Assert.Equal(SslMode.VerifyFull, connection.SslMode);
    }

    [Fact]
    public void MapsTheRedisUrlToATlsConnection()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["REDIS_URL"] = "rediss://default:test%40secret@redis.example.com:6379"
        });

        MarketplaceConnections.Apply(configuration);

        var connection = ConfigurationOptions.Parse(configuration.GetConnectionString("Redis")!);
        var endpoint = Assert.IsType<DnsEndPoint>(Assert.Single(connection.EndPoints));
        Assert.Equal("redis.example.com", endpoint.Host);
        Assert.Equal(6379, endpoint.Port);
        Assert.Equal("default", connection.User);
        Assert.Equal("test@secret", connection.Password);
        Assert.True(connection.Ssl);
    }

    [Fact]
    public void KeepsExplicitRailwayAndLocalConnections()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=postgres.railway.internal",
            ["ConnectionStrings:Redis"] = "redis.railway.internal:6379",
            ["PGHOST_UNPOOLED"] = "direct.example.com",
            ["REDIS_URL"] = "rediss://default:test@redis.example.com:6379"
        });

        MarketplaceConnections.Apply(configuration);

        Assert.Equal("Host=postgres.railway.internal", configuration.GetConnectionString("Postgres"));
        Assert.Equal("redis.railway.internal:6379", configuration.GetConnectionString("Redis"));
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
