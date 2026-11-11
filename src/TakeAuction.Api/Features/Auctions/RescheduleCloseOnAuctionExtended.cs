using MediatR;
using TakeAuction.Api.Common.Jobs;
using TakeAuction.Api.Features.Auctions.ExpireAuctions;
using TakeAuction.Api.Features.Auctions.PlaceBid;

namespace TakeAuction.Api.Features.Auctions;

public sealed class RescheduleCloseOnAuctionExtended : INotificationHandler<BidPlacedEvent>
{
    private readonly IAuctionCloseSchedule? _schedule;
    private readonly DurableAuctionScheduler? _durable;

    public RescheduleCloseOnAuctionExtended(IAuctionCloseSchedule? schedule = null, DurableAuctionScheduler? durable = null)
    {
        _schedule = schedule;
        _durable = durable;
    }

    public Task Handle(BidPlacedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.AuctionExtended)
        {
            if (_durable?.Enabled == true)
            {
                return _durable.ScheduleAsync("close", notification.AuctionId, notification.EndsAtUtc);
            }

            (_schedule ?? throw new InvalidOperationException("The auction closing scheduler is not registered.")).ScheduleClose(
                notification.AuctionId,
                notification.EndsAtUtc,
                notification.OccurredAtUtc);
        }

        return Task.CompletedTask;
    }
}
