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
    public async Task Sends_a_bid_to_both_the_auction_group_and_the_lobby()
    {
        _clients
            .Groups(Arg.Is<IReadOnlyList<string>>(groups =>
                groups.Contains(AuctionHub.AuctionGroup(AuctionId))
                && groups.Contains(AuctionHub.LobbyGroup)))
            .Returns(_target);

        var notification = BidNotification();

        await _notifier.BidPlacedAsync(notification, CancellationToken.None);

        await _target.Received(1).BidPlaced(notification);
    }

    [Fact]
    public async Task Does_not_leak_a_bid_to_another_auctions_group()
    {
        var otherAuction = Guid.CreateVersion7();
        var otherTarget = Substitute.For<IAuctionClient>();

        _clients
            .Groups(Arg.Is<IReadOnlyList<string>>(groups =>
                groups.Contains(AuctionHub.AuctionGroup(otherAuction))))
            .Returns(otherTarget);

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
    public async Task Tells_the_bidder_who_lost_the_lead_and_nobody_else()
    {
        var outbidBidderId = Guid.CreateVersion7();
        var everyoneElse = Substitute.For<IAuctionClient>();

        _clients.User(outbidBidderId.ToString()).Returns(_target);
        _clients.All.Returns(everyoneElse);
        _clients.Group(Arg.Any<string>()).Returns(everyoneElse);

        var notification = OutbidNotice();

        await _notifier.OutbidAsync(outbidBidderId, notification, CancellationToken.None);

        await _target.Received(1).Outbid(notification);
        await everyoneElse.DidNotReceive().Outbid(Arg.Any<OutbidNotification>());
    }

    [Fact]
    public async Task Addresses_the_bidder_by_the_id_signalr_knows_them_by()
    {
        var outbidBidderId = Guid.CreateVersion7();
        _clients.User(Arg.Any<string>()).Returns(_target);

        await _notifier.OutbidAsync(outbidBidderId, OutbidNotice(), CancellationToken.None);

        _clients.Received(1).User(outbidBidderId.ToString());
    }

    [Fact]
    public async Task Honours_cancellation_before_telling_a_bidder_they_lost_the_lead()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _clients.User(Arg.Any<string>()).Returns(_target);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _notifier.OutbidAsync(Guid.CreateVersion7(), OutbidNotice(), cancellation.Token));

        await _target.DidNotReceive().Outbid(Arg.Any<OutbidNotification>());
    }

    [Fact]
    public async Task Honours_cancellation_before_broadcasting()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _clients.Groups(Arg.Any<IReadOnlyList<string>>()).Returns(_target);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _notifier.BidPlacedAsync(BidNotification(), cancellation.Token));

        await _target.DidNotReceive().BidPlaced(Arg.Any<BidPlacedNotification>());
    }

    private static BidPlacedNotification BidNotification() => new(
        AuctionId,
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        150m,
        false,
        100m,
        null,
        TestHarness.Now.AddDays(2),
        false,
        TestHarness.Now);

    private static OutbidNotification OutbidNotice() => new(
        AuctionId,
        "Rare stamp collection",
        155m,
        TestHarness.Now.AddDays(2),
        TestHarness.Now);

    private static AuctionStatusChangedNotification StatusNotification() => new(
        AuctionId,
        "Active",
        100m,
        null,
        TestHarness.Now.AddDays(2),
        TestHarness.Now);
}
