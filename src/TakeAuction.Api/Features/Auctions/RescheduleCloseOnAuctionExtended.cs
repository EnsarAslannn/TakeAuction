using MediatR;
using TakeAuction.Api.Features.Auctions.ExpireAuctions;
using TakeAuction.Api.Features.Auctions.PlaceBid;

namespace TakeAuction.Api.Features.Auctions;

public sealed class RescheduleCloseOnAuctionExtended : INotificationHandler<BidPlacedEvent>
{
    private readonly IAuctionCloseSchedule _schedule;

    public RescheduleCloseOnAuctionExtended(IAuctionCloseSchedule schedule) => _schedule = schedule;

    public Task Handle(BidPlacedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.AuctionExtended)
        {
            _schedule.ScheduleClose(
                notification.AuctionId,
                notification.EndsAtUtc,
                notification.OccurredAtUtc);
        }

        return Task.CompletedTask;
    }
}
