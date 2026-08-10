using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TakeAuction.Api.Common.RealTime;

namespace TakeAuction.Api.UnitTests.Common.RealTime;

public sealed class AuctionHubTests : IDisposable
{
    private const string ConnectionId = "connection-1";

    private static readonly Guid AuctionId = Guid.CreateVersion7();

    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly AuctionHub _hub;

    public AuctionHubTests()
    {
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns(ConnectionId);

        _hub = new AuctionHub(NullLogger<AuctionHub>.Instance)
        {
            Groups = _groups,
            Context = context
        };
    }

    [Fact]
    public void Scopes_every_auction_to_its_own_group()
    {
        var other = Guid.CreateVersion7();

        Assert.Equal($"auction:{AuctionId}", AuctionHub.AuctionGroup(AuctionId));
        Assert.NotEqual(AuctionHub.AuctionGroup(AuctionId), AuctionHub.AuctionGroup(other));
        Assert.NotEqual(AuctionHub.LobbyGroup, AuctionHub.AuctionGroup(AuctionId));
    }

    [Fact]
    public async Task Subscribing_joins_the_auction_group()
    {
        await _hub.SubscribeToAuction(AuctionId);

        await _groups.Received(1).AddToGroupAsync(
            ConnectionId,
            AuctionHub.AuctionGroup(AuctionId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unsubscribing_leaves_the_auction_group()
    {
        await _hub.UnsubscribeFromAuction(AuctionId);

        await _groups.Received(1).RemoveFromGroupAsync(
            ConnectionId,
            AuctionHub.AuctionGroup(AuctionId),
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

    public void Dispose() => _hub.Dispose();
}
