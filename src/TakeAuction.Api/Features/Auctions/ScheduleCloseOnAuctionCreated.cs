using MediatR;
using TakeAuction.Api.Features.Auctions.CreateAuction;
using TakeAuction.Api.Features.Auctions.ExpireAuctions;

namespace TakeAuction.Api.Features.Auctions;

public sealed class ScheduleCloseOnAuctionCreated : INotificationHandler<AuctionCreatedEvent>
{
    private readonly IAuctionCloseSchedule _schedule;

    public ScheduleCloseOnAuctionCreated(IAuctionCloseSchedule schedule) => _schedule = schedule;

    public Task Handle(AuctionCreatedEvent notification, CancellationToken cancellationToken)
    {
        _schedule.ScheduleClose(notification.AuctionId, notification.EndsAtUtc, notification.OccurredAtUtc);

        return Task.CompletedTask;
    }
}
