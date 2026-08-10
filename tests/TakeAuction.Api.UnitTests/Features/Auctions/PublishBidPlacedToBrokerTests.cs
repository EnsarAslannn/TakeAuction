using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.Features.Auctions.PlaceBid;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions;

public sealed class PublishBidPlacedToBrokerTests
{
    private static readonly Guid AuctionId = Guid.CreateVersion7();
    private static readonly Guid BidId = Guid.CreateVersion7();
    private static readonly Guid BidderId = Guid.CreateVersion7();
    private static readonly Guid OutbidBidderId = Guid.CreateVersion7();

    private readonly IPublishEndpoint _publishEndpoint = Substitute.For<IPublishEndpoint>();
    private readonly PublishBidPlacedToBroker _handler;

    public PublishBidPlacedToBrokerTests() =>
        _handler = new PublishBidPlacedToBroker(
            _publishEndpoint,
            NullLogger<PublishBidPlacedToBroker>.Instance);

    [Fact]
    public async Task Maps_every_field_of_the_domain_event_onto_the_wire_contract()
    {
        await _handler.Handle(Event(), CancellationToken.None);

        await _publishEndpoint.Received(1).Publish(
            Arg.Is<BidPlacedIntegrationEvent>(message =>
                message.AuctionId == AuctionId
                && message.BidId == BidId
                && message.BidderId == BidderId
                && message.Amount == 150m
                && message.PreviousPrice == 100m
                && message.OutbidBidderId == OutbidBidderId
                && message.EndsAtUtc == TestHarness.Now.AddDays(2)
                && message.OccurredAtUtc == TestHarness.Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Keeps_a_missing_outbid_leader_null()
    {
        await _handler.Handle(EventWith(outbidBidderId: null), CancellationToken.None);

        await _publishEndpoint.Received(1).Publish(
            Arg.Is<BidPlacedIntegrationEvent>(message => message.OutbidBidderId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Swallows_a_broker_outage_so_the_committed_bid_still_returns_a_success()
    {
        _publishEndpoint
            .Publish(Arg.Any<BidPlacedIntegrationEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("RabbitMQ is unreachable"));

        var exception = await Record.ExceptionAsync(() => _handler.Handle(Event(), CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Still_surfaces_cancellation()
    {
        _publishEndpoint
            .Publish(Arg.Any<BidPlacedIntegrationEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _handler.Handle(Event(), CancellationToken.None));
    }

    private static BidPlacedEvent Event() => EventWith(OutbidBidderId);

    private static BidPlacedEvent EventWith(Guid? outbidBidderId) => new(
        AuctionId,
        BidId,
        BidderId,
        150m,
        100m,
        outbidBidderId,
        TestHarness.Now.AddDays(2),
        TestHarness.Now);
}
