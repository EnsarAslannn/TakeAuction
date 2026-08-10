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
        BidId,
        BidderId,
        150m,
        100m,
        null,
        TestHarness.Now.AddDays(2),
        TestHarness.Now);
}
