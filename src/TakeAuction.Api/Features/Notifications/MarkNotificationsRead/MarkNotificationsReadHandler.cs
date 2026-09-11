using MediatR;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.Features.Notifications.MarkNotificationsRead;

public sealed class MarkNotificationsReadHandler : IRequestHandler<MarkNotificationsReadCommand, int>
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public MarkNotificationsReadHandler(AppDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public Task<int> Handle(MarkNotificationsReadCommand command, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        return _dbContext.Notifications
            .Where(notification => notification.UserId == command.UserId && notification.ReadAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(notification => notification.ReadAtUtc, now),
                cancellationToken);
    }
}
