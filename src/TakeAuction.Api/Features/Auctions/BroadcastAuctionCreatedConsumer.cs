using MassTransit;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.RealTime;

namespace TakeAuction.Api.Features.Auctions;

public sealed class BroadcastAuctionCreatedConsumer : IConsumer<AuctionCreatedIntegrationEvent>
{
    private readonly IAuctionNotifier _notifier;
    private readonly ILogger<BroadcastAuctionCreatedConsumer> _logger;

    public BroadcastAuctionCreatedConsumer(
        IAuctionNotifier notifier,
        ILogger<BroadcastAuctionCreatedConsumer> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AuctionCreatedIntegrationEvent> context)
    {
        var message = context.Message;

        await _notifier.AuctionStatusChangedAsync(
            new AuctionStatusChangedNotification(
                message.AuctionId,
                message.Status,
                message.StartingPrice,
                message.EndsAtUtc,
                message.OccurredAtUtc),
            context.CancellationToken);

        _logger.LogInformation(
            "Broadcast status {Status} for auction {AuctionId} to real-time watchers",
            message.Status,
            message.AuctionId);
    }
}
