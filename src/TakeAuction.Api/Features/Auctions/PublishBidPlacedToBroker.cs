using MassTransit;
using MediatR;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Features.Auctions.PlaceBid;

namespace TakeAuction.Api.Features.Auctions;

public sealed class PublishBidPlacedToBroker : INotificationHandler<BidPlacedEvent>
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PublishBidPlacedToBroker> _logger;

    public PublishBidPlacedToBroker(
        IPublishEndpoint publishEndpoint,
        ILogger<PublishBidPlacedToBroker> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(BidPlacedEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new BidPlacedIntegrationEvent(
            notification.AuctionId,
            notification.BidId,
            notification.BidderId,
            notification.Amount,
            notification.PreviousPrice,
            notification.OutbidBidderId,
            notification.EndsAtUtc,
            notification.OccurredAtUtc);

        try
        {
            await _publishEndpoint.Publish(integrationEvent, cancellationToken);

            _logger.LogInformation(
                "Published BidPlacedIntegrationEvent for bid {BidId} on auction {AuctionId}",
                notification.BidId,
                notification.AuctionId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Failed to publish BidPlacedIntegrationEvent for bid {BidId}; the bid is committed and remains authoritative",
                notification.BidId);
        }
    }
}
