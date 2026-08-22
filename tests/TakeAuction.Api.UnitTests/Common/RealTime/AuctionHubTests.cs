using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Common.RealTime;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.UnitTests.Common.RealTime;

public sealed class AuctionHubTests : IAsyncLifetime
{
    private const string ConnectionId = "connection-1";

    private const int SubscriptionLimit = 3;

    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly AppDbContext _dbContext = TestHarness.CreateDbContext();
    private readonly AuctionHub _hub;

    private Guid _auctionId;

    public AuctionHubTests()
    {
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns(ConnectionId);
        context.Items.Returns(new Dictionary<object, object?>());

        _hub = new AuctionHub(
            _dbContext,
            Options.Create(new RealTimeOptions { MaxAuctionSubscriptionsPerConnection = SubscriptionLimit }),
            NullLogger<AuctionHub>.Instance)
        {
            Groups = _groups,
            Context = context
        };
    }

    public async Task InitializeAsync() => _auctionId = await AddAuctionAsync();

    public Task DisposeAsync()
    {
        _hub.Dispose();

        return _dbContext.DisposeAsync().AsTask();
    }

    [Fact]
    public void Scopes_every_auction_to_its_own_group()
    {
        var other = Guid.CreateVersion7();

        Assert.Equal($"auction:{_auctionId}", AuctionHub.AuctionGroup(_auctionId));
        Assert.NotEqual(AuctionHub.AuctionGroup(_auctionId), AuctionHub.AuctionGroup(other));
        Assert.NotEqual(AuctionHub.LobbyGroup, AuctionHub.AuctionGroup(_auctionId));
    }

    [Fact]
    public async Task Subscribing_joins_the_auction_group()
    {
        await _hub.SubscribeToAuction(_auctionId);

        await _groups.Received(1).AddToGroupAsync(
            ConnectionId,
            AuctionHub.AuctionGroup(_auctionId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_auction_that_does_not_exist_is_refused()
    {
        var unknown = Guid.CreateVersion7();

        await Assert.ThrowsAsync<HubException>(() => _hub.SubscribeToAuction(unknown));

        await _groups.DidNotReceive().AddToGroupAsync(
            ConnectionId,
            AuctionHub.AuctionGroup(unknown),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_empty_auction_id_is_refused_without_touching_the_database()
    {
        await Assert.ThrowsAsync<HubException>(() => _hub.SubscribeToAuction(Guid.Empty));

        await _groups.DidNotReceive().AddToGroupAsync(
            ConnectionId,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_connection_cannot_watch_more_auctions_than_the_configured_limit()
    {
        for (var index = 0; index < SubscriptionLimit; index++)
        {
            await _hub.SubscribeToAuction(await AddAuctionAsync());
        }

        var oneTooMany = await AddAuctionAsync();

        await Assert.ThrowsAsync<HubException>(() => _hub.SubscribeToAuction(oneTooMany));

        await _groups.Received(SubscriptionLimit).AddToGroupAsync(
            ConnectionId,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Subscribing_twice_to_the_same_auction_joins_the_group_once()
    {
        await _hub.SubscribeToAuction(_auctionId);
        await _hub.SubscribeToAuction(_auctionId);

        await _groups.Received(1).AddToGroupAsync(
            ConnectionId,
            AuctionHub.AuctionGroup(_auctionId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unsubscribing_frees_a_slot_under_the_limit()
    {
        var subscribed = new List<Guid>();

        for (var index = 0; index < SubscriptionLimit; index++)
        {
            var auctionId = await AddAuctionAsync();
            subscribed.Add(auctionId);
            await _hub.SubscribeToAuction(auctionId);
        }

        await _hub.UnsubscribeFromAuction(subscribed[0]);

        var replacement = await AddAuctionAsync();

        await _hub.SubscribeToAuction(replacement);

        await _groups.Received(1).AddToGroupAsync(
            ConnectionId,
            AuctionHub.AuctionGroup(replacement),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unsubscribing_leaves_the_auction_group()
    {
        await _hub.UnsubscribeFromAuction(_auctionId);

        await _groups.Received(1).RemoveFromGroupAsync(
            ConnectionId,
            AuctionHub.AuctionGroup(_auctionId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Lobby_membership_is_opt_in()
    {
        await _hub.SubscribeToLobby();

        await _groups.Received(1).AddToGroupAsync(
            ConnectionId,
            AuctionHub.LobbyGroup,
            Arg.Any<CancellationToken>());

        await _hub.UnsubscribeFromLobby();

        await _groups.Received(1).RemoveFromGroupAsync(
            ConnectionId,
            AuctionHub.LobbyGroup,
            Arg.Any<CancellationToken>());
    }

    private async Task<Guid> AddAuctionAsync()
    {
        var auction = Auction.Create(
            Guid.CreateVersion7(),
            "A lot worth watching",
            "A detailed description of the lot on offer.",
            100m,
            5m,
            TestHarness.Now,
            TestHarness.Now.AddHours(2),
            TestHarness.Now);

        _dbContext.Auctions.Add(auction);
        await _dbContext.SaveChangesAsync();

        return auction.Id;
    }
}
