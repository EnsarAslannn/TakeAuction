using MediatR;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Auctions.CreateAuction;
using TakeAuction.Api.Features.Auctions.OpenAuctions;

namespace TakeAuction.Api.Features.Auctions;

public sealed class ScheduleOpenOnAuctionCreated : INotificationHandler<AuctionCreatedEvent>
{
    private readonly IAuctionOpenSchedule _schedule;

    public ScheduleOpenOnAuctionCreated(IAuctionOpenSchedule schedule) => _schedule = schedule;

    public Task Handle(AuctionCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.Status == nameof(AuctionStatus.Scheduled))
        {
            _schedule.ScheduleOpen(
                notification.AuctionId,
                notification.StartsAtUtc,
                notification.OccurredAtUtc);
        }

        return Task.CompletedTask;
    }
}
