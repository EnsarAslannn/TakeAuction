using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.Features.Auctions.CreateAuction;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions;

public sealed class PublishAuctionCreatedToBrokerTests
{
    private static readonly Guid AuctionId = Guid.CreateVersion7();
    private static readonly Guid SellerId = Guid.CreateVersion7();

    private readonly IPublishEndpoint _publishEndpoint = Substitute.For<IPublishEndpoint>();
    private readonly PublishAuctionCreatedToBroker _handler;

    public PublishAuctionCreatedToBrokerTests() =>
        _handler = new PublishAuctionCreatedToBroker(
            _publishEndpoint,
            NullLogger<PublishAuctionCreatedToBroker>.Instance);

    [Fact]
    public async Task Maps_the_domain_event_onto_the_wire_contract()
    {
        await _handler.Handle(Event(), CancellationToken.None);

        await _publishEndpoint.Received(1).Publish(
            Arg.Is<AuctionCreatedIntegrationEvent>(message =>
                message.AuctionId == AuctionId
                && message.SellerId == SellerId
                && message.StartingPrice == 100m
                && message.Status == nameof(AuctionStatus.Active)
                && message.EndsAtUtc == TestHarness.Now.AddDays(2)
                && message.OccurredAtUtc == TestHarness.Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Carries_a_scheduled_auction_through_unchanged()
    {
        await _handler.Handle(Event(nameof(AuctionStatus.Scheduled)), CancellationToken.None);

        await _publishEndpoint.Received(1).Publish(
            Arg.Is<AuctionCreatedIntegrationEvent>(message =>
                message.Status == nameof(AuctionStatus.Scheduled)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Swallows_a_broker_outage_so_the_committed_auction_still_returns_a_success()
    {
        _publishEndpoint
            .Publish(Arg.Any<AuctionCreatedIntegrationEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("RabbitMQ is unreachable"));

        var exception = await Record.ExceptionAsync(() => _handler.Handle(Event(), CancellationToken.None));

        Assert.Null(exception);
    }

    private static AuctionCreatedEvent Event(string? status = null) => new(
        AuctionId,
        SellerId,
        100m,
        status ?? nameof(AuctionStatus.Active),
        TestHarness.Now.AddDays(2),
        TestHarness.Now);
}
