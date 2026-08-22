using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;

namespace TakeAuction.Api.Common.Api;

public static class ForwardedHeadersExtensions
{
    public const string ConfigurationSection = "ReverseProxy";

    public static readonly string[] PrivateNetworks =
    [
        "127.0.0.0/8",
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16",
        "::1/128",
        "fc00::/7"
    ];

    public static IServiceCollection AddTakeAuctionForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var knownProxies = configuration.GetSection($"{ConfigurationSection}:KnownProxies").Get<string[]>() ?? [];
        var knownNetworks = configuration.GetSection($"{ConfigurationSection}:KnownNetworks").Get<string[]>() ?? [];

        if (knownProxies.Length == 0 && knownNetworks.Length == 0)
        {
            knownNetworks = PrivateNetworks;

            Log.Information(
                "No reverse proxy was configured under '{Section}', so forwarded headers are trusted from the "
                + "private address ranges only. A proxy that reaches this service from a public address must be "
                + "named explicitly, or every anonymous caller will share one rate limiting partition",
                ConfigurationSection);
        }

        var forwardLimit = configuration.GetValue<int?>($"{ConfigurationSection}:ForwardLimit") ?? 1;

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

            options.ForwardLimit = forwardLimit;

            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var proxy in knownProxies)
            {
                if (IPAddress.TryParse(proxy, out var address))
                {
                    options.KnownProxies.Add(address);
                }
                else
                {
                    Log.Warning("Ignoring '{Proxy}' under '{Section}:KnownProxies': it is not an IP address", proxy, ConfigurationSection);
                }
            }

            foreach (var network in knownNetworks)
            {
                if (TryParseNetwork(network, out var parsed))
                {
                    options.KnownIPNetworks.Add(parsed);
                }
                else
                {
                    Log.Warning("Ignoring '{Network}' under '{Section}:KnownNetworks': it is not a CIDR range", network, ConfigurationSection);
                }
            }
        });

        return services;
    }

    private static bool TryParseNetwork(string value, out System.Net.IPNetwork network)
    {
        network = default;

        var parts = value.Split('/', 2);

        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out var prefix)
            || !int.TryParse(parts[1], out var prefixLength))
        {
            return false;
        }

        try
        {
            network = new System.Net.IPNetwork(prefix, prefixLength);

            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
