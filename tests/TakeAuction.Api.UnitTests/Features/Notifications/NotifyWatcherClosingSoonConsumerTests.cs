using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Common.RealTime;
using TakeAuction.Api.Domain.Notifications;
using TakeAuction.Api.Features.Notifications;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Notifications;

public sealed class NotifyWatcherClosingSoonConsumerTests : IDisposable
{
    private static readonly Guid WatcherId = Guid.CreateVersion7();
    private static readonly Guid AuctionId = Guid.CreateVersion7();

    private readonly AppDbContext _dbContext = TestHarness.CreateDbContext();
    private readonly IAuctionNotifier _notifier = Substitute.For<IAuctionNotifier>();

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Files_a_closing_soon_reminder_in_the_watchers_inbox()
    {
        await Consumer().Consume(Context(Reminder()));

        var notification = Assert.Single(await _dbContext.Notifications.AsNoTracking().ToListAsync());

        Assert.Equal(WatcherId, notification.UserId);
        Assert.Equal(NotificationKind.AuctionClosingSoon, notification.Kind);
        Assert.Equal("Rare stamp collection", notification.AuctionTitle);
        Assert.Equal(TestHarness.Now.AddMinutes(5), notification.AuctionEndsAtUtc);
        Assert.Null(notification.Amount);
    }

    [Fact]
    public async Task Pushes_the_reminder_to_the_watcher()
    {
        await Consumer().Consume(Context(Reminder()));

        await _notifier.Received(1).NotifyUserAsync(
            WatcherId,
            Arg.Is<UserNotification>(n => n.Kind == nameof(NotificationKind.AuctionClosingSoon)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reminds_a_watcher_once_however_often_the_message_arrives()
    {
        await Consumer().Consume(Context(Reminder()));
        await Consumer().Consume(Context(Reminder()));

        Assert.Equal(1, await _dbContext.Notifications.CountAsync());
        await _notifier.Received(1).NotifyUserAsync(
            Arg.Any<Guid>(), Arg.Any<UserNotification>(), Arg.Any<CancellationToken>());
    }

    private NotifyWatcherClosingSoonConsumer Consumer() =>
        new(
            new NotificationInbox(_dbContext, _notifier, NullLogger<NotificationInbox>.Instance),
            new FixedTimeProvider(TestHarness.Now));

    private static WatchedAuctionClosingSoonIntegrationEvent Reminder() => new(
        WatcherId,
        AuctionId,
        "Rare stamp collection",
        TestHarness.Now.AddMinutes(5),
        TestHarness.Now);

    private static ConsumeContext<WatchedAuctionClosingSoonIntegrationEvent> Context(
        WatchedAuctionClosingSoonIntegrationEvent message)
    {
        var context = Substitute.For<ConsumeContext<WatchedAuctionClosingSoonIntegrationEvent>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);

        return context;
    }
}
