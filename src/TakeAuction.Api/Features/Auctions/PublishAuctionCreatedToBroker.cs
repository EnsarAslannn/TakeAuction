using MassTransit;
using MediatR;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Features.Auctions.CreateAuction;

namespace TakeAuction.Api.Features.Auctions;

public sealed class PublishAuctionCreatedToBroker : INotificationHandler<AuctionCreatedEvent>
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PublishAuctionCreatedToBroker> _logger;

    public PublishAuctionCreatedToBroker(
        IPublishEndpoint publishEndpoint,
        ILogger<PublishAuctionCreatedToBroker> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(AuctionCreatedEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new AuctionCreatedIntegrationEvent(
            notification.AuctionId,
            notification.SellerId,
            notification.StartingPrice,
            notification.Status,
            notification.EndsAtUtc,
            notification.OccurredAtUtc);

        try
        {
            await _publishEndpoint.Publish(integrationEvent, cancellationToken);

            _logger.LogInformation(
                "Published AuctionCreatedIntegrationEvent for auction {AuctionId}",
                notification.AuctionId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Failed to publish AuctionCreatedIntegrationEvent for auction {AuctionId}; the auction is committed and remains authoritative",
                notification.AuctionId);
        }
    }
}
