using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.Features.Auctions.ExpireAuctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions;

public sealed class PublishAuctionEndedToBrokerTests
{
    private static readonly Guid AuctionId = Guid.CreateVersion7();
    private static readonly Guid SellerId = Guid.CreateVersion7();
    private static readonly Guid WinnerId = Guid.CreateVersion7();

    private readonly IPublishEndpoint _publishEndpoint = Substitute.For<IPublishEndpoint>();
    private readonly PublishAuctionEndedToBroker _handler;

    public PublishAuctionEndedToBrokerTests() =>
        _handler = new PublishAuctionEndedToBroker(
            _publishEndpoint,
            NullLogger<PublishAuctionEndedToBroker>.Instance);

    [Fact]
    public async Task Maps_every_field_of_the_domain_event_onto_the_wire_contract()
    {
        await _handler.Handle(EventWith(WinnerId, 250m, 4), CancellationToken.None);

        await _publishEndpoint.Received(1).Publish(
            Arg.Is<AuctionEndedIntegrationEvent>(message =>
                message.AuctionId == AuctionId
                && message.SellerId == SellerId
                && message.WinningBidderId == WinnerId
                && message.FinalPrice == 250m
                && message.BidCount == 4
                && message.EndsAtUtc == TestHarness.Now
                && message.OccurredAtUtc == TestHarness.Now.AddSeconds(30)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Carries_an_unsold_auction_through_without_a_winner()
    {
        await _handler.Handle(EventWith(null, 100m, 0), CancellationToken.None);

        await _publishEndpoint.Received(1).Publish(
            Arg.Is<AuctionEndedIntegrationEvent>(message =>
                message.WinningBidderId == null && message.BidCount == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Swallows_a_broker_outage_because_the_auction_is_already_closed()
    {
        _publishEndpoint
            .Publish(Arg.Any<AuctionEndedIntegrationEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("RabbitMQ is unreachable"));

        var exception = await Record.ExceptionAsync(
            () => _handler.Handle(EventWith(WinnerId, 250m, 4), CancellationToken.None));

        Assert.Null(exception);
    }

    private static AuctionEndedEvent EventWith(Guid? winningBidderId, decimal finalPrice, int bidCount) => new(
        AuctionId,
        SellerId,
        winningBidderId,
        finalPrice,
        bidCount,
        TestHarness.Now,
        TestHarness.Now.AddSeconds(30));
}
