using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TakeAuction.Api.Common.RealTime;

namespace TakeAuction.Api.UnitTests.Common.RealTime;

public sealed class SignalRAuctionNotifierTests
{
    private static readonly Guid AuctionId = Guid.CreateVersion7();

    private readonly IHubClients<IAuctionClient> _clients = Substitute.For<IHubClients<IAuctionClient>>();
    private readonly IAuctionClient _target = Substitute.For<IAuctionClient>();
    private readonly SignalRAuctionNotifier _notifier;

    public SignalRAuctionNotifierTests()
    {
        var hubContext = Substitute.For<IHubContext<AuctionHub, IAuctionClient>>();
        hubContext.Clients.Returns(_clients);

        _notifier = new SignalRAuctionNotifier(hubContext, NullLogger<SignalRAuctionNotifier>.Instance);
    }

    [Fact]
    public async Task Sends_a_bid_only_to_watchers_of_that_auction()
    {
        _clients.Group(AuctionHub.AuctionGroup(AuctionId)).Returns(_target);

        var notification = BidNotification();

        await _notifier.BidPlacedAsync(notification, CancellationToken.None);

        await _target.Received(1).BidPlaced(notification);
        _clients.Received(1).Group(AuctionHub.AuctionGroup(AuctionId));
    }

    [Fact]
    public async Task Does_not_leak_a_bid_to_another_auctions_group()
    {
        var otherAuction = Guid.CreateVersion7();
        var otherTarget = Substitute.For<IAuctionClient>();

        _clients.Group(AuctionHub.AuctionGroup(AuctionId)).Returns(_target);
        _clients.Group(AuctionHub.AuctionGroup(otherAuction)).Returns(otherTarget);

        await _notifier.BidPlacedAsync(BidNotification(), CancellationToken.None);

        await otherTarget.DidNotReceive().BidPlaced(Arg.Any<BidPlacedNotification>());
    }

    [Fact]
    public async Task Sends_a_status_change_to_both_the_auction_group_and_the_lobby()
    {
        _clients
            .Groups(Arg.Is<IReadOnlyList<string>>(groups =>
                groups.Contains(AuctionHub.AuctionGroup(AuctionId))
                && groups.Contains(AuctionHub.LobbyGroup)))
            .Returns(_target);

        var notification = StatusNotification();

        await _notifier.AuctionStatusChangedAsync(notification, CancellationToken.None);

        await _target.Received(1).AuctionStatusChanged(notification);
    }

    [Fact]
    public async Task Honours_cancellation_before_broadcasting()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _clients.Group(Arg.Any<string>()).Returns(_target);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _notifier.BidPlacedAsync(BidNotification(), cancellation.Token));

        await _target.DidNotReceive().BidPlaced(Arg.Any<BidPlacedNotification>());
    }

    private static BidPlacedNotification BidNotification() => new(
        AuctionId,
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        150m,
        100m,
        null,
        TestHarness.Now.AddDays(2),
        TestHarness.Now);

    private static AuctionStatusChangedNotification StatusNotification() => new(
        AuctionId,
        "Active",
        100m,
        TestHarness.Now.AddDays(2),
        TestHarness.Now);
}
