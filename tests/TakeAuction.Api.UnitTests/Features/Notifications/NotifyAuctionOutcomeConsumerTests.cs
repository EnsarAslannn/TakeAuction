using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Common.RealTime;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Notifications;
using TakeAuction.Api.Features.Notifications;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Notifications;

public sealed class NotifyAuctionOutcomeConsumerTests : IDisposable
{
    private static readonly Guid SellerId = Guid.CreateVersion7();
    private static readonly Guid WinnerId = Guid.CreateVersion7();

    private readonly AppDbContext _dbContext = TestHarness.CreateDbContext();
    private readonly IAuctionNotifier _notifier = Substitute.For<IAuctionNotifier>();
    private readonly Auction _auction;

    public NotifyAuctionOutcomeConsumerTests()
    {
        _auction = Auction.Create(
            SellerId,
            "Rare stamp collection",
            "A lot under test.",
            100m,
            5m,
            TestHarness.Now.AddHours(-2),
            TestHarness.Now.AddMinutes(-1),
            TestHarness.Now.AddHours(-3));

        _dbContext.Auctions.Add(_auction);
        _dbContext.SaveChanges();
        _dbContext.ChangeTracker.Clear();
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Tells_the_winner_they_won_and_the_seller_that_it_sold()
    {
        await Consumer().Consume(Context(Ended(WinnerId)));

        var stored = await Stored();

        Assert.Equal(2, stored.Count);
        Assert.Contains(stored, n => n.UserId == WinnerId && n.Kind == NotificationKind.AuctionWon && n.Amount == 250m);
        Assert.Contains(stored, n => n.UserId == SellerId && n.Kind == NotificationKind.AuctionSold && n.Amount == 250m);
        Assert.All(stored, n => Assert.Equal("Rare stamp collection", n.AuctionTitle));
    }

    [Fact]
    public async Task Tells_only_the_seller_when_nobody_bid()
    {
        await Consumer().Consume(Context(Ended(null)));

        var notification = Assert.Single(await Stored());

        Assert.Equal(SellerId, notification.UserId);
        Assert.Equal(NotificationKind.AuctionUnsold, notification.Kind);
        Assert.Null(notification.Amount);
    }

    [Fact]
    public async Task Pushes_each_new_notification_to_its_recipient()
    {
        await Consumer().Consume(Context(Ended(WinnerId)));

        await _notifier.Received(1).NotifyUserAsync(
            WinnerId,
            Arg.Is<UserNotification>(n => n.Kind == nameof(NotificationKind.AuctionWon) && n.AuctionId == _auction.Id),
            Arg.Any<CancellationToken>());

        await _notifier.Received(1).NotifyUserAsync(
            SellerId,
            Arg.Is<UserNotification>(n => n.Kind == nameof(NotificationKind.AuctionSold)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_redelivered_close_neither_duplicates_the_inbox_nor_pushes_again()
    {
        var message = Ended(WinnerId);

        await Consumer().Consume(Context(message));
        _notifier.ClearReceivedCalls();

        await Consumer().Consume(Context(message));

        Assert.Equal(2, (await Stored()).Count);
        await _notifier.DidNotReceiveWithAnyArgs().NotifyUserAsync(default, default!, default);
    }

    [Fact]
    public async Task Keeps_the_notification_when_the_push_fails()
    {
        _notifier
            .NotifyUserAsync(Arg.Any<Guid>(), Arg.Any<UserNotification>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("The backplane is down."));

        await Consumer().Consume(Context(Ended(WinnerId)));

        Assert.Equal(2, (await Stored()).Count);
    }

    [Fact]
    public async Task Tells_nobody_about_an_auction_that_no_longer_exists()
    {
        var message = Ended(WinnerId) with { AuctionId = Guid.CreateVersion7() };

        await Consumer().Consume(Context(message));

        Assert.Empty(await Stored());
        await _notifier.DidNotReceiveWithAnyArgs().NotifyUserAsync(default, default!, default);
    }

    private NotifyAuctionOutcomeConsumer Consumer() =>
        new(
            _dbContext,
            new NotificationInbox(_dbContext, _notifier, NullLogger<NotificationInbox>.Instance),
            new FixedTimeProvider(TestHarness.Now),
            NullLogger<NotifyAuctionOutcomeConsumer>.Instance);

    private Task<List<Notification>> Stored() =>
        _dbContext.Notifications.AsNoTracking().ToListAsync();

    private AuctionEndedIntegrationEvent Ended(Guid? winnerId) => new(
        _auction.Id,
        SellerId,
        winnerId,
        winnerId is null ? 100m : 250m,
        winnerId is null ? 0 : 4,
        _auction.EndsAtUtc,
        TestHarness.Now);

    private static ConsumeContext<AuctionEndedIntegrationEvent> Context(AuctionEndedIntegrationEvent message)
    {
        var context = Substitute.For<ConsumeContext<AuctionEndedIntegrationEvent>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);

        return context;
    }
}
