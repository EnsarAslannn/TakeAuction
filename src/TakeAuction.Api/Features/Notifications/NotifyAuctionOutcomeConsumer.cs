using MassTransit;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Notifications;

namespace TakeAuction.Api.Features.Notifications;

public sealed class NotifyAuctionOutcomeConsumer : IConsumer<AuctionEndedIntegrationEvent>
{
    private readonly AppDbContext _dbContext;
    private readonly NotificationInbox _inbox;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotifyAuctionOutcomeConsumer> _logger;

    public NotifyAuctionOutcomeConsumer(
        AppDbContext dbContext,
        NotificationInbox inbox,
        TimeProvider timeProvider,
        ILogger<NotifyAuctionOutcomeConsumer> logger)
    {
        _dbContext = dbContext;
        _inbox = inbox;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AuctionEndedIntegrationEvent> context)
    {
        var message = context.Message;

        var title = await _dbContext.Auctions
            .AsNoTracking()
            .Where(auction => auction.Id == message.AuctionId)
            .Select(auction => auction.Title)
            .FirstOrDefaultAsync(context.CancellationToken);

        if (title is null)
        {
            _logger.LogWarning(
                "Auction {AuctionId} ended but no longer exists; nobody is told about its outcome",
                message.AuctionId);

            return;
        }

        var delivered = await _inbox.DeliverAsync(Outcome(message, title), context.CancellationToken);

        _logger.LogInformation(
            "Told {Delivered} participant(s) how auction {AuctionId} ended",
            delivered,
            message.AuctionId);
    }

    private Notification[] Outcome(AuctionEndedIntegrationEvent message, string title)
    {
        var now = _timeProvider.GetUtcNow();

        if (message.WinningBidderId is not { } winnerId)
        {
            return
            [
                Notification.Create(
                    message.SellerId,
                    NotificationKind.AuctionUnsold,
                    message.AuctionId,
                    title,
                    null,
                    message.EndsAtUtc,
                    now)
            ];
        }

        return
        [
            Notification.Create(
                winnerId,
                NotificationKind.AuctionWon,
                message.AuctionId,
                title,
                message.FinalPrice,
                message.EndsAtUtc,
                now),
            Notification.Create(
                message.SellerId,
                NotificationKind.AuctionSold,
                message.AuctionId,
                title,
                message.FinalPrice,
                message.EndsAtUtc,
                now)
        ];
    }
}
