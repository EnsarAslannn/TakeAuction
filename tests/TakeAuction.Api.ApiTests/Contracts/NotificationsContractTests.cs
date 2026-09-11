using System.Net;
using TakeAuction.Api.ApiTests.Common;
using TakeAuction.Api.Domain.Notifications;
using TakeAuction.Api.Features.Notifications.GetNotifications;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class NotificationsContractTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;

    private ApiSession _seller = null!;
    private ApiSession _bidder = null!;
    private Guid _auctionId;

    public NotificationsContractTests(ApiTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _seller = await _fixture.CreateSellerAsync("Notified Seller");
        _bidder = await _fixture.CreateBidderAsync("Notified Bidder");
        _auctionId = await _fixture.CreateOpenAuctionAsync(_seller);
    }

    public Task DisposeAsync()
    {
        _seller.Dispose();
        _bidder.Dispose();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Anonymous_callers_are_challenged()
    {
        using var client = _fixture.CreateRawClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(ApiRoutes.Notifications)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(ApiRoutes.NotificationsRead, null)).StatusCode);
    }

    [Fact]
    public async Task An_empty_inbox_reads_as_nothing_unread()
    {
        var inbox = await InboxAsync(_bidder);

        Assert.Empty(inbox.Items);
        Assert.Equal(0, inbox.UnreadCount);
    }

    [Fact]
    public async Task Lists_the_callers_notifications_newest_first_with_the_unread_count()
    {
        await SeedAsync(_bidder.UserId, NotificationKind.AuctionClosingSoon, DateTimeOffset.UtcNow.AddMinutes(-10));
        await SeedAsync(_bidder.UserId, NotificationKind.AuctionWon, DateTimeOffset.UtcNow);

        var inbox = await InboxAsync(_bidder);

        Assert.Equal(2, inbox.UnreadCount);
        Assert.Equal(["AuctionWon", "AuctionClosingSoon"], inbox.Items.Select(item => item.Kind));
        Assert.All(inbox.Items, item =>
        {
            Assert.Equal(_auctionId, item.AuctionId);
            Assert.Null(item.ReadAtUtc);
        });
    }

    [Fact]
    public async Task Never_shows_one_callers_notifications_to_another()
    {
        await SeedAsync(_seller.UserId, NotificationKind.AuctionSold, DateTimeOffset.UtcNow);

        var inbox = await InboxAsync(_bidder);

        Assert.Empty(inbox.Items);
        Assert.Equal(0, inbox.UnreadCount);
    }

    [Fact]
    public async Task Marking_read_clears_the_unread_count_and_keeps_the_items()
    {
        await SeedAsync(_bidder.UserId, NotificationKind.AuctionWon, DateTimeOffset.UtcNow);

        var response = await _bidder.PostAsync(ApiRoutes.NotificationsRead, new { });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var inbox = await InboxAsync(_bidder);
        Assert.Equal(0, inbox.UnreadCount);
        Assert.NotNull(Assert.Single(inbox.Items).ReadAtUtc);
    }

    [Fact]
    public async Task Marking_read_leaves_another_callers_inbox_unread()
    {
        await SeedAsync(_seller.UserId, NotificationKind.AuctionSold, DateTimeOffset.UtcNow);

        await _bidder.PostAsync(ApiRoutes.NotificationsRead, new { });

        Assert.Equal(1, (await InboxAsync(_seller)).UnreadCount);
    }

    [Fact]
    public async Task Marking_read_without_the_csrf_token_is_refused()
    {
        await SeedAsync(_bidder.UserId, NotificationKind.AuctionWon, DateTimeOffset.UtcNow);

        var response = await _bidder.PostAsync(ApiRoutes.NotificationsRead, new { }, withCsrf: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, (await InboxAsync(_bidder)).UnreadCount);
    }

    private static async Task<NotificationsResponse> InboxAsync(ApiSession session)
    {
        var response = await session.GetAsync(ApiRoutes.Notifications);
        response.EnsureSuccessStatusCode();

        return await session.ReadAsync<NotificationsResponse>(response);
    }

    private Task SeedAsync(Guid userId, NotificationKind kind, DateTimeOffset createdAt) =>
        _fixture.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.Notifications.Add(Notification.Create(
                userId,
                kind,
                _auctionId,
                "Live lot under test",
                kind == NotificationKind.AuctionClosingSoon ? null : 150m,
                DateTimeOffset.UtcNow.AddHours(2),
                createdAt));

            return await dbContext.SaveChangesAsync();
        });
}
