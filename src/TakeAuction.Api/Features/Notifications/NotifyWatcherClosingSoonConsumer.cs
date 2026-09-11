using MassTransit;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Domain.Notifications;

namespace TakeAuction.Api.Features.Notifications;

public sealed class NotifyWatcherClosingSoonConsumer : IConsumer<WatchedAuctionClosingSoonIntegrationEvent>
{
    private readonly NotificationInbox _inbox;
    private readonly TimeProvider _timeProvider;

    public NotifyWatcherClosingSoonConsumer(NotificationInbox inbox, TimeProvider timeProvider)
    {
        _inbox = inbox;
        _timeProvider = timeProvider;
    }

    public Task Consume(ConsumeContext<WatchedAuctionClosingSoonIntegrationEvent> context)
    {
        var message = context.Message;

        var reminder = Notification.Create(
            message.UserId,
            NotificationKind.AuctionClosingSoon,
            message.AuctionId,
            message.AuctionTitle,
            null,
            message.EndsAtUtc,
            _timeProvider.GetUtcNow());

        return _inbox.DeliverAsync([reminder], context.CancellationToken);
    }
}
