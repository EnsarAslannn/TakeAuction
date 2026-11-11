using MediatR;
using TakeAuction.Api.Common.Jobs;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Auctions.CreateAuction;
using TakeAuction.Api.Features.Auctions.OpenAuctions;

namespace TakeAuction.Api.Features.Auctions;

public sealed class ScheduleOpenOnAuctionCreated : INotificationHandler<AuctionCreatedEvent>
{
    private readonly IAuctionOpenSchedule? _schedule;
    private readonly DurableAuctionScheduler? _durable;

    public ScheduleOpenOnAuctionCreated(IAuctionOpenSchedule? schedule = null, DurableAuctionScheduler? durable = null)
    {
        _schedule = schedule;
        _durable = durable;
    }

    public Task Handle(AuctionCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.Status == nameof(AuctionStatus.Scheduled))
        {
            if (_durable?.Enabled == true)
            {
                return _durable.ScheduleAsync("open", notification.AuctionId, notification.StartsAtUtc);
            }

            (_schedule ?? throw new InvalidOperationException("The auction opening scheduler is not registered.")).ScheduleOpen(
                notification.AuctionId,
                notification.StartsAtUtc,
                notification.OccurredAtUtc);
        }

        return Task.CompletedTask;
    }
}
