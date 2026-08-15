using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.RealTime;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions;

public sealed class BroadcastBidPlacedConsumerTests
{
    private static readonly Guid AuctionId = Guid.CreateVersion7();
    private static readonly Guid BidId = Guid.CreateVersion7();
    private static readonly Guid BidderId = Guid.CreateVersion7();

    private readonly IAuctionNotifier _notifier = Substitute.For<IAuctionNotifier>();
    private readonly BroadcastBidPlacedConsumer _consumer;

    public BroadcastBidPlacedConsumerTests() =>
        _consumer = new BroadcastBidPlacedConsumer(
            _notifier,
            NullLogger<BroadcastBidPlacedConsumer>.Instance);

    [Fact]
    public async Task Broadcasts_the_bid_that_arrived_on_the_queue()
    {
        await _consumer.Consume(Context(Message()));

        await _notifier.Received(1).BidPlacedAsync(
            Arg.Is<BidPlacedNotification>(notification =>
                notification.AuctionId == AuctionId
                && notification.BidId == BidId
                && notification.BidderId == BidderId
                && notification.Amount == 150m
                && notification.PreviousPrice == 100m
                && notification.OccurredAtUtc == TestHarness.Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Tells_the_bidder_who_lost_the_lead()
    {
        var outbidBidderId = Guid.CreateVersion7();

        await _consumer.Consume(Context(Message() with { OutbidBidderId = outbidBidderId }));

        await _notifier.Received(1).OutbidAsync(
            outbidBidderId,
            Arg.Is<OutbidNotification>(notification =>
                notification.AuctionId == AuctionId
                && notification.AuctionTitle == "Rare stamp collection"
                && notification.CurrentPrice == 150m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Says_nothing_to_anybody_when_the_lead_did_not_change_hands()
    {
        await _consumer.Consume(Context(Message()));

        await _notifier.DidNotReceive().OutbidAsync(
            Arg.Any<Guid>(),
            Arg.Any<OutbidNotification>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Broadcasts_to_the_lot_before_it_taps_anyone_on_the_shoulder()
    {
        var outbidBidderId = Guid.CreateVersion7();

        await _consumer.Consume(Context(Message() with { OutbidBidderId = outbidBidderId }));

        Received.InOrder(() =>
        {
            _notifier.BidPlacedAsync(Arg.Any<BidPlacedNotification>(), Arg.Any<CancellationToken>());
            _notifier.OutbidAsync(outbidBidderId, Arg.Any<OutbidNotification>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Lets_a_broadcast_failure_bubble_up_so_the_message_is_retried()
    {
        _notifier
            .BidPlacedAsync(Arg.Any<BidPlacedNotification>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("backplane unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _consumer.Consume(Context(Message())));
    }

    private static ConsumeContext<BidPlacedIntegrationEvent> Context(BidPlacedIntegrationEvent message)
    {
        var context = Substitute.For<ConsumeContext<BidPlacedIntegrationEvent>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);

        return context;
    }

    private static BidPlacedIntegrationEvent Message() => new(
        AuctionId,
        "Rare stamp collection",
        BidId,
        BidderId,
        150m,
        false,
        100m,
        null,
        TestHarness.Now.AddDays(2),
        false,
        TestHarness.Now);
}
