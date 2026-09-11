using MediatR;
using TakeAuction.Api.Common.RealTime;

namespace TakeAuction.Api.Features.Notifications.GetNotifications;

public sealed record GetNotificationsQuery(Guid UserId, int Take = GetNotificationsQuery.DefaultTake)
    : IRequest<NotificationsResponse>
{
    public const int DefaultTake = 20;

    public const int MaxTake = 50;

    public int NormalizedTake => Take switch
    {
        < 1 => DefaultTake,
        > MaxTake => MaxTake,
        _ => Take
    };
}

public sealed record NotificationsResponse(IReadOnlyList<UserNotification> Items, int UnreadCount);
