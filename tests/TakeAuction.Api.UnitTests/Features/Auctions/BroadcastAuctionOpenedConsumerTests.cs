using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.RealTime;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions;

public sealed class BroadcastAuctionOpenedConsumerTests
{
    private static readonly Guid AuctionId = Guid.CreateVersion7();

    private readonly IAuctionNotifier _notifier = Substitute.For<IAuctionNotifier>();
    private readonly BroadcastAuctionOpenedConsumer _consumer;

    public BroadcastAuctionOpenedConsumerTests() =>
        _consumer = new BroadcastAuctionOpenedConsumer(
            _notifier,
            NullLogger<BroadcastAuctionOpenedConsumer>.Instance);

    [Fact]
    public async Task Broadcasts_the_opening_as_an_active_status_change()
    {
        await _consumer.Consume(Context());

        await _notifier.Received(1).AuctionStatusChangedAsync(
            Arg.Is<AuctionStatusChangedNotification>(notification =>
                notification.AuctionId == AuctionId
                && notification.Status == nameof(AuctionStatus.Active)
                && notification.CurrentPrice == 100m
                && notification.EndsAtUtc == TestHarness.Now.AddDays(1)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reports_no_leading_bidder_because_a_lot_opens_without_one()
    {
        await _consumer.Consume(Context());

        await _notifier.Received(1).AuctionStatusChangedAsync(
            Arg.Is<AuctionStatusChangedNotification>(notification => notification.LeadingBidderId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Never_broadcasts_a_bid_for_an_opening_message()
    {
        await _consumer.Consume(Context());

        await _notifier.DidNotReceive().BidPlacedAsync(
            Arg.Any<BidPlacedNotification>(),
            Arg.Any<CancellationToken>());
    }

    private static ConsumeContext<AuctionOpenedIntegrationEvent> Context()
    {
        var context = Substitute.For<ConsumeContext<AuctionOpenedIntegrationEvent>>();
        context.Message.Returns(Message());
        context.CancellationToken.Returns(CancellationToken.None);

        return context;
    }

    private static AuctionOpenedIntegrationEvent Message() => new(
        AuctionId,
        Guid.CreateVersion7(),
        100m,
        TestHarness.Now,
        TestHarness.Now.AddDays(1),
        TestHarness.Now);
}
