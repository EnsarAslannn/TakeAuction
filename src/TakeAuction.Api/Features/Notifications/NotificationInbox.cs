using Microsoft.EntityFrameworkCore;
using Npgsql;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Common.RealTime;
using TakeAuction.Api.Domain.Notifications;

namespace TakeAuction.Api.Features.Notifications;

public sealed class NotificationInbox
{
    private readonly AppDbContext _dbContext;
    private readonly IAuctionNotifier _notifier;
    private readonly ILogger<NotificationInbox> _logger;

    public NotificationInbox(AppDbContext dbContext, IAuctionNotifier notifier, ILogger<NotificationInbox> logger)
    {
        _dbContext = dbContext;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<int> DeliverAsync(
        IReadOnlyCollection<Notification> notifications,
        CancellationToken cancellationToken)
    {
        var delivered = new List<Notification>(notifications.Count);

        foreach (var notification in notifications)
        {
            if (await StoreAsync(notification, cancellationToken))
            {
                delivered.Add(notification);
            }
        }

        foreach (var notification in delivered)
        {
            await PushAsync(notification, cancellationToken);
        }

        return delivered.Count;
    }

    public static UserNotification ToMessage(Notification notification) => new(
        notification.Id,
        notification.Kind.ToString(),
        notification.AuctionId,
        notification.AuctionTitle,
        notification.Amount,
        notification.AuctionEndsAtUtc,
        notification.CreatedAtUtc,
        notification.ReadAtUtc);

    private async Task<bool> StoreAsync(Notification notification, CancellationToken cancellationToken)
    {
        var alreadyStored = await _dbContext.Notifications
            .AsNoTracking()
            .AnyAsync(
                stored => stored.UserId == notification.UserId
                    && stored.Kind == notification.Kind
                    && stored.AuctionId == notification.AuctionId,
                cancellationToken);

        if (alreadyStored)
        {
            return false;
        }

        _dbContext.Notifications.Add(notification);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            _dbContext.Entry(notification).State = EntityState.Detached;

            _logger.LogDebug(
                "A {Kind} notification about auction {AuctionId} for user {UserId} was stored by a concurrent delivery",
                notification.Kind,
                notification.AuctionId,
                notification.UserId);

            return false;
        }
    }

    private async Task PushAsync(Notification notification, CancellationToken cancellationToken)
    {
        try
        {
            await _notifier.NotifyUserAsync(notification.UserId, ToMessage(notification), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Stored a {Kind} notification for user {UserId} but could not push it; it waits in their inbox",
                notification.Kind,
                notification.UserId);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
