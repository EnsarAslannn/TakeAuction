using MediatR;
using TakeAuction.Api.Common.Jobs;
using TakeAuction.Api.Features.Auctions.CreateAuction;
using TakeAuction.Api.Features.Auctions.ExpireAuctions;

namespace TakeAuction.Api.Features.Auctions;

public sealed class ScheduleCloseOnAuctionCreated : INotificationHandler<AuctionCreatedEvent>
{
    private readonly IAuctionCloseSchedule? _schedule;
    private readonly DurableAuctionScheduler? _durable;

    public ScheduleCloseOnAuctionCreated(IAuctionCloseSchedule? schedule = null, DurableAuctionScheduler? durable = null)
    {
        _schedule = schedule;
        _durable = durable;
    }

    public Task Handle(AuctionCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (_durable?.Enabled == true)
        {
            return _durable.ScheduleAsync("close", notification.AuctionId, notification.EndsAtUtc);
        }

        (_schedule ?? throw new InvalidOperationException("The auction closing scheduler is not registered."))
            .ScheduleClose(notification.AuctionId, notification.EndsAtUtc, notification.OccurredAtUtc);

        return Task.CompletedTask;
    }
}
