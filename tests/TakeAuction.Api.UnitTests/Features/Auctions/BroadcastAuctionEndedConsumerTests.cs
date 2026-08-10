using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.RealTime;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions;

public sealed class BroadcastAuctionEndedConsumerTests
{
    private static readonly Guid AuctionId = Guid.CreateVersion7();
    private static readonly Guid WinnerId = Guid.CreateVersion7();

    private readonly IAuctionNotifier _notifier = Substitute.For<IAuctionNotifier>();
    private readonly BroadcastAuctionEndedConsumer _consumer;

    public BroadcastAuctionEndedConsumerTests() =>
        _consumer = new BroadcastAuctionEndedConsumer(
            _notifier,
            NullLogger<BroadcastAuctionEndedConsumer>.Instance);

    [Fact]
    public async Task Broadcasts_the_close_as_an_ended_status_change()
    {
        await _consumer.Consume(Context(MessageWith(WinnerId)));

        await _notifier.Received(1).AuctionStatusChangedAsync(
            Arg.Is<AuctionStatusChangedNotification>(notification =>
                notification.AuctionId == AuctionId
                && notification.Status == nameof(AuctionStatus.Ended)
                && notification.CurrentPrice == 250m
                && notification.LeadingBidderId == WinnerId
                && notification.EndsAtUtc == TestHarness.Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reports_no_leading_bidder_for_an_unsold_auction()
    {
        await _consumer.Consume(Context(MessageWith(null)));

        await _notifier.Received(1).AuctionStatusChangedAsync(
            Arg.Is<AuctionStatusChangedNotification>(notification => notification.LeadingBidderId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Never_broadcasts_a_bid_for_a_close_message()
    {
        await _consumer.Consume(Context(MessageWith(WinnerId)));

        await _notifier.DidNotReceive().BidPlacedAsync(
            Arg.Any<BidPlacedNotification>(),
            Arg.Any<CancellationToken>());
    }

    private static ConsumeContext<AuctionEndedIntegrationEvent> Context(AuctionEndedIntegrationEvent message)
    {
        var context = Substitute.For<ConsumeContext<AuctionEndedIntegrationEvent>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);

        return context;
    }

    private static AuctionEndedIntegrationEvent MessageWith(Guid? winningBidderId) => new(
        AuctionId,
        Guid.CreateVersion7(),
        winningBidderId,
        250m,
        4,
        TestHarness.Now,
        TestHarness.Now.AddSeconds(30));
}
