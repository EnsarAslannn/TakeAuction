using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace TakeAuction.Api.Common.Api;

public static class ForwardedHeadersExtensions
{
    public const string ConfigurationSection = "ReverseProxy";

    public static IServiceCollection AddTakeAuctionForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var knownProxies = configuration.GetSection($"{ConfigurationSection}:KnownProxies").Get<string[]>() ?? [];
        var knownNetworks = configuration.GetSection($"{ConfigurationSection}:KnownNetworks").Get<string[]>() ?? [];

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
            }

            foreach (var network in knownNetworks)
            {
                var parts = network.Split('/', 2);
                if (parts.Length == 2
                    && IPAddress.TryParse(parts[0], out var prefix)
                    && int.TryParse(parts[1], out var prefixLength))
                {
                    options.KnownIPNetworks.Add(new System.Net.IPNetwork(prefix, prefixLength));
                }
            }
        });

        return services;
    }
}
