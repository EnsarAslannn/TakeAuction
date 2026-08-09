using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Caching;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Features.Auctions;

public sealed class AuctionCache
{
    private const string GenerationKey = "auctions:list:generation";
    private static readonly TimeSpan GenerationTtl = TimeSpan.FromDays(7);

    private readonly ICacheService _cache;
    private readonly CacheOptions _options;

    public AuctionCache(ICacheService cache, IOptions<CacheOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public TimeSpan ListTtl => TimeSpan.FromSeconds(_options.AuctionListTtlSeconds);

    public TimeSpan DetailTtl => TimeSpan.FromSeconds(_options.AuctionDetailTtlSeconds);

    public static string DetailKey(Guid auctionId) => $"auctions:detail:{auctionId}";

    public async Task<string> GetListGenerationAsync(CancellationToken cancellationToken)
    {
        var generation = await _cache.GetAsync<string>(GenerationKey, cancellationToken);
        if (!string.IsNullOrEmpty(generation))
        {
            return generation;
        }

        var created = NewGenerationToken();
        await _cache.SetAsync(GenerationKey, created, GenerationTtl, cancellationToken);
        return created;
    }

    public Task InvalidateListsAsync(CancellationToken cancellationToken) =>
        _cache.SetAsync(GenerationKey, NewGenerationToken(), GenerationTtl, cancellationToken);

    public Task InvalidateDetailAsync(Guid auctionId, CancellationToken cancellationToken) =>
        _cache.RemoveAsync(DetailKey(auctionId), cancellationToken);

    public static string ListKey(
        string generation,
        int page,
        int pageSize,
        AuctionStatus? status,
        Guid? sellerId,
        string? search)
    {
        var builder = new StringBuilder("auctions:list:")
            .Append(generation)
            .Append(":p").Append(page.ToString(CultureInfo.InvariantCulture))
            .Append(":s").Append(pageSize.ToString(CultureInfo.InvariantCulture))
            .Append(":st").Append(status?.ToString() ?? "any")
            .Append(":sl").Append(sellerId?.ToString("N") ?? "any")
            .Append(":q").Append(HashSearchTerm(search));

        return builder.ToString();
    }

    private static string NewGenerationToken() => Guid.CreateVersion7().ToString("N");

    private static string HashSearchTerm(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return "none";
        }

        var normalized = search.Trim().ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(hash.AsSpan(0, 8));
    }
}
