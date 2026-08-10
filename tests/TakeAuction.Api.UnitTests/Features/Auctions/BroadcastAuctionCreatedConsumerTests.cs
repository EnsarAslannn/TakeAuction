using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.RealTime;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions;

public sealed class BroadcastAuctionCreatedConsumerTests
{
    private static readonly Guid AuctionId = Guid.CreateVersion7();

    private readonly IAuctionNotifier _notifier = Substitute.For<IAuctionNotifier>();
    private readonly BroadcastAuctionCreatedConsumer _consumer;

    public BroadcastAuctionCreatedConsumerTests() =>
        _consumer = new BroadcastAuctionCreatedConsumer(
            _notifier,
            NullLogger<BroadcastAuctionCreatedConsumer>.Instance);

    [Fact]
    public async Task Announces_a_new_auction_as_a_status_change()
    {
        await _consumer.Consume(Context(Message()));

        await _notifier.Received(1).AuctionStatusChangedAsync(
            Arg.Is<AuctionStatusChangedNotification>(notification =>
                notification.AuctionId == AuctionId
                && notification.Status == nameof(AuctionStatus.Active)
                && notification.CurrentPrice == 100m
                && notification.EndsAtUtc == TestHarness.Now.AddDays(2)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Never_broadcasts_a_bid_for_a_creation_message()
    {
        await _consumer.Consume(Context(Message()));

        await _notifier.DidNotReceive().BidPlacedAsync(
            Arg.Any<BidPlacedNotification>(),
            Arg.Any<CancellationToken>());
    }

    private static ConsumeContext<AuctionCreatedIntegrationEvent> Context(
        AuctionCreatedIntegrationEvent message)
    {
        var context = Substitute.For<ConsumeContext<AuctionCreatedIntegrationEvent>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);

        return context;
    }

    private static AuctionCreatedIntegrationEvent Message() => new(
        AuctionId,
        Guid.CreateVersion7(),
        100m,
        nameof(AuctionStatus.Active),
        TestHarness.Now.AddDays(2),
        TestHarness.Now);
}
