using MediatR;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Common.RealTime;

namespace TakeAuction.Api.Features.Notifications.GetNotifications;

public sealed class GetNotificationsHandler : IRequestHandler<GetNotificationsQuery, NotificationsResponse>
{
    private readonly AppDbContext _dbContext;

    public GetNotificationsHandler(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<NotificationsResponse> Handle(GetNotificationsQuery query, CancellationToken cancellationToken)
    {
        var mine = _dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == query.UserId);

        var items = await mine
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .ThenByDescending(notification => notification.Id)
            .Take(query.NormalizedTake)
            .Select(notification => new UserNotification(
                notification.Id,
                notification.Kind.ToString(),
                notification.AuctionId,
                notification.AuctionTitle,
                notification.Amount,
                notification.AuctionEndsAtUtc,
                notification.CreatedAtUtc,
                notification.ReadAtUtc))
            .ToListAsync(cancellationToken);

        var unreadCount = await mine.CountAsync(notification => notification.ReadAtUtc == null, cancellationToken);

        return new NotificationsResponse(items, unreadCount);
    }
}
