using MassTransit;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.RealTime;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Features.Auctions;

public sealed class BroadcastAuctionEndedConsumer : IConsumer<AuctionEndedIntegrationEvent>
{
    private readonly IAuctionNotifier _notifier;
    private readonly ILogger<BroadcastAuctionEndedConsumer> _logger;

    public BroadcastAuctionEndedConsumer(
        IAuctionNotifier notifier,
        ILogger<BroadcastAuctionEndedConsumer> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AuctionEndedIntegrationEvent> context)
    {
        var message = context.Message;

        await _notifier.AuctionStatusChangedAsync(
            new AuctionStatusChangedNotification(
                message.AuctionId,
                nameof(AuctionStatus.Ended),
                message.FinalPrice,
                message.WinningBidderId,
                message.EndsAtUtc,
                message.OccurredAtUtc),
            context.CancellationToken);

        _logger.LogInformation(
            "Broadcast the close of auction {AuctionId} to real-time watchers",
            message.AuctionId);
    }
}
