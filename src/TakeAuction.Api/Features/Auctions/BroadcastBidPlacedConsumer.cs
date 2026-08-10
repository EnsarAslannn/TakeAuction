using MassTransit;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.RealTime;

namespace TakeAuction.Api.Features.Auctions;

public sealed class BroadcastBidPlacedConsumer : IConsumer<BidPlacedIntegrationEvent>
{
    private readonly IAuctionNotifier _notifier;
    private readonly ILogger<BroadcastBidPlacedConsumer> _logger;

    public BroadcastBidPlacedConsumer(IAuctionNotifier notifier, ILogger<BroadcastBidPlacedConsumer> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<BidPlacedIntegrationEvent> context)
    {
        var message = context.Message;

        await _notifier.BidPlacedAsync(
            new BidPlacedNotification(
                message.AuctionId,
                message.BidId,
                message.BidderId,
                message.Amount,
                message.PreviousPrice,
                message.OutbidBidderId,
                message.EndsAtUtc,
                message.OccurredAtUtc),
            context.CancellationToken);

        _logger.LogInformation(
            "Broadcast bid {BidId} on auction {AuctionId} to real-time watchers",
            message.BidId,
            message.AuctionId);
    }
}
