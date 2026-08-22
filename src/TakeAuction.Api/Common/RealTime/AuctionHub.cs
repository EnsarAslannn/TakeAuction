using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.Common.RealTime;

[AllowAnonymous]
public sealed class AuctionHub : Hub<IAuctionClient>
{
    public const string Route = "/hubs/auctions";

    public const string LobbyGroup = "auctions:lobby";

    private const string SubscriptionsKey = "takeauction:auction-subscriptions";

    private readonly AppDbContext _dbContext;
    private readonly RealTimeOptions _options;
    private readonly ILogger<AuctionHub> _logger;

    public AuctionHub(
        AppDbContext dbContext,
        IOptions<RealTimeOptions> options,
        ILogger<AuctionHub> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public static string AuctionGroup(Guid auctionId) => $"auction:{auctionId}";

    public async Task SubscribeToAuction(Guid auctionId)
    {
        if (auctionId == Guid.Empty)
        {
            throw new HubException("An auction id is required.");
        }

        var subscriptions = Subscriptions();

        if (!subscriptions.Add(auctionId))
        {
            return;
        }

        if (subscriptions.Count > _options.MaxAuctionSubscriptionsPerConnection)
        {
            subscriptions.Remove(auctionId);

            _logger.LogWarning(
                "Connection {ConnectionId} tried to watch more than {Limit} auctions at once",
                Context.ConnectionId,
                _options.MaxAuctionSubscriptionsPerConnection);

            throw new HubException(
                $"A connection may watch at most {_options.MaxAuctionSubscriptionsPerConnection} auctions at once.");
        }

        var exists = await _dbContext.Auctions
            .AsNoTracking()
            .AnyAsync(auction => auction.Id == auctionId, Context.ConnectionAborted);

        if (!exists)
        {
            subscriptions.Remove(auctionId);

            throw new HubException($"No auction exists with id '{auctionId}'.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, AuctionGroup(auctionId), Context.ConnectionAborted);

        _logger.LogDebug(
            "Connection {ConnectionId} subscribed to auction {AuctionId}",
            Context.ConnectionId,
            auctionId);
    }

    public async Task UnsubscribeFromAuction(Guid auctionId)
    {
        Subscriptions().Remove(auctionId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AuctionGroup(auctionId), Context.ConnectionAborted);

        _logger.LogDebug(
            "Connection {ConnectionId} unsubscribed from auction {AuctionId}",
            Context.ConnectionId,
            auctionId);
    }

    public Task SubscribeToLobby() =>
        Groups.AddToGroupAsync(Context.ConnectionId, LobbyGroup, Context.ConnectionAborted);

    public Task UnsubscribeFromLobby() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, LobbyGroup, Context.ConnectionAborted);

    private HashSet<Guid> Subscriptions()
    {
        if (Context.Items.TryGetValue(SubscriptionsKey, out var stored) && stored is HashSet<Guid> existing)
        {
            return existing;
        }

        var created = new HashSet<Guid>();
        Context.Items[SubscriptionsKey] = created;

        return created;
    }
}
