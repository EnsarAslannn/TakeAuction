using MassTransit;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.RealTime;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Features.Auctions;

public sealed class BroadcastAuctionCancelledConsumer : IConsumer<AuctionCancelledIntegrationEvent>
{
    private readonly IAuctionNotifier _notifier;
    private readonly ILogger<BroadcastAuctionCancelledConsumer> _logger;

    public BroadcastAuctionCancelledConsumer(
        IAuctionNotifier notifier,
        ILogger<BroadcastAuctionCancelledConsumer> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AuctionCancelledIntegrationEvent> context)
    {
        var message = context.Message;

        await _notifier.AuctionStatusChangedAsync(
            new AuctionStatusChangedNotification(
                message.AuctionId,
                nameof(AuctionStatus.Cancelled),
                message.CurrentPrice,
                null,
                message.EndsAtUtc,
                message.OccurredAtUtc),
            context.CancellationToken);

        _logger.LogInformation(
            "Broadcast the withdrawal of auction {AuctionId} to real-time watchers",
            message.AuctionId);
    }
}
