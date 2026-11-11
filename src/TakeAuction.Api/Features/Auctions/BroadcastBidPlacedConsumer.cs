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

    public Task Consume(ConsumeContext<BidPlacedIntegrationEvent> context) =>
        HandleAsync(context.Message, context.CancellationToken);

    public async Task HandleAsync(BidPlacedIntegrationEvent message, CancellationToken cancellationToken)
    {

        await _notifier.BidPlacedAsync(
            new BidPlacedNotification(
                message.AuctionId,
                message.BidId,
                message.BidderId,
                message.Amount,
                message.Automatic,
                message.PreviousPrice,
                message.OutbidBidderId,
                message.EndsAtUtc,
                message.AuctionExtended,
                message.OccurredAtUtc),
            cancellationToken);

        _logger.LogInformation(
            "Broadcast bid {BidId} on auction {AuctionId} to real-time watchers",
            message.BidId,
            message.AuctionId);

        if (message.OutbidBidderId is not { } outbidBidderId)
        {
            return;
        }

        await _notifier.OutbidAsync(
            outbidBidderId,
            new OutbidNotification(
                message.AuctionId,
                message.AuctionTitle,
                message.Amount,
                message.EndsAtUtc,
                message.OccurredAtUtc),
            cancellationToken);
    }
}
