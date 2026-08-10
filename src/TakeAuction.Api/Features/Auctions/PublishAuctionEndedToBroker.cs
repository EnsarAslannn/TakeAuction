using MassTransit;
using MediatR;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Features.Auctions.ExpireAuctions;

namespace TakeAuction.Api.Features.Auctions;

public sealed class PublishAuctionEndedToBroker : INotificationHandler<AuctionEndedEvent>
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PublishAuctionEndedToBroker> _logger;

    public PublishAuctionEndedToBroker(
        IPublishEndpoint publishEndpoint,
        ILogger<PublishAuctionEndedToBroker> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(AuctionEndedEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new AuctionEndedIntegrationEvent(
            notification.AuctionId,
            notification.SellerId,
            notification.WinningBidderId,
            notification.FinalPrice,
            notification.BidCount,
            notification.EndsAtUtc,
            notification.OccurredAtUtc);

        try
        {
            await _publishEndpoint.Publish(integrationEvent, cancellationToken);

            _logger.LogInformation(
                "Published AuctionEndedIntegrationEvent for auction {AuctionId}",
                notification.AuctionId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Failed to publish AuctionEndedIntegrationEvent for auction {AuctionId}; the auction is already closed in the database",
                notification.AuctionId);
        }
    }
}
