using MediatR;
using TakeAuction.Api.Features.Auctions.ExpireAuctions;
using TakeAuction.Api.Features.Auctions.PlaceBid;

namespace TakeAuction.Api.Features.Auctions;

/// <summary>
/// A bid in the closing seconds moves the end, which leaves the booked close pointing at a
/// second that no longer means anything. Booking another one for the new time is what keeps a
/// contested lot closing on time instead of falling back to the sweep.
/// </summary>
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
