using MassTransit;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.RealTime;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Features.Auctions;

public sealed class BroadcastAuctionOpenedConsumer : IConsumer<AuctionOpenedIntegrationEvent>
{
    private readonly IAuctionNotifier _notifier;
    private readonly ILogger<BroadcastAuctionOpenedConsumer> _logger;

    public BroadcastAuctionOpenedConsumer(
        IAuctionNotifier notifier,
        ILogger<BroadcastAuctionOpenedConsumer> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AuctionOpenedIntegrationEvent> context)
    {
        var message = context.Message;

        await _notifier.AuctionStatusChangedAsync(
            new AuctionStatusChangedNotification(
                message.AuctionId,
                nameof(AuctionStatus.Active),
                message.CurrentPrice,
                LeadingBidderId: null,
                message.EndsAtUtc,
                message.OccurredAtUtc),
            context.CancellationToken);

        _logger.LogInformation(
            "Broadcast the opening of auction {AuctionId} to real-time watchers",
            message.AuctionId);
    }
}
