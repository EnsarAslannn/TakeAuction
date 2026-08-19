using System.Net.Http.Json;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.RealTime;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Features.Auctions.CreateAuction;
using TakeAuction.Api.Features.Auctions.PlaceBid;
using TakeAuction.Api.IntegrationTests.Common;

namespace TakeAuction.Api.IntegrationTests.Features.Auctions;

[Collection(IntegrationTestCollection.Name)]
public sealed class AuctionRealTimeTests : IAsyncLifetime
{
    private const decimal StartingPrice = 100m;
    private const decimal Increment = 5m;

    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SilenceWindow = TimeSpan.FromSeconds(3);

    private readonly IntegrationTestFixture _fixture;

    private User _seller = null!;
    private Guid _auctionId;

    public AuctionRealTimeTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _seller = await _fixture.CreateUserAsync(UserRole.Seller);
        _auctionId = await CreateAuctionAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Runs_against_a_real_rabbitmq_broker()
    {
        var bus = _fixture.Services.GetRequiredService<IBus>();

        Assert.StartsWith("rabbitmq://", bus.Address.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Fans_hub_messages_out_through_the_redis_backplane()
    {
        var lifetimeManager = _fixture.Services.GetRequiredService<HubLifetimeManager<AuctionHub>>();

        Assert.IsType<RedisHubLifetimeManager<AuctionHub>>(lifetimeManager);
    }

    [Fact]
    public async Task A_bid_travels_from_the_api_through_rabbitmq_to_a_subscribed_watcher()
    {
        await using var connection = _fixture.CreateHubConnection();
        var received = Capture<BidPlacedNotification>(connection, nameof(IAuctionClient.BidPlaced));

        await connection.StartAsync();
        await connection.InvokeAsync(nameof(AuctionHub.SubscribeToAuction), _auctionId);

        var bidder = await _fixture.CreateUserAsync(UserRole.Bidder);
        var client = await _fixture.CreateClientAsAsync(bidder);

        var response = await client.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(150m));
        response.EnsureSuccessStatusCode();

        var notification = await received.Task.WaitAsync(DeliveryTimeout);

        Assert.Equal(_auctionId, notification.AuctionId);
        Assert.Equal(bidder.Id, notification.BidderId);
        Assert.Equal(StartingPrice, notification.Amount);
        Assert.False(notification.Automatic);
        Assert.Equal(StartingPrice, notification.PreviousPrice);
        Assert.Null(notification.OutbidBidderId);
    }

    [Fact]
    public async Task A_proxy_answering_for_the_leader_reaches_watchers_as_an_automatic_bid()
    {
        var leader = await _fixture.CreateUserAsync(UserRole.Bidder);
        var leaderClient = await _fixture.CreateClientAsAsync(leader);
        (await leaderClient.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(500m)))
            .EnsureSuccessStatusCode();

        await _fixture.WaitForOutboxDrainAsync();

        await using var connection = _fixture.CreateHubConnection();
        var received = Capture<BidPlacedNotification>(connection, nameof(IAuctionClient.BidPlaced));

        await connection.StartAsync();
        await connection.InvokeAsync(nameof(AuctionHub.SubscribeToAuction), _auctionId);

        var challenger = await _fixture.CreateUserAsync(UserRole.Bidder);
        var challengerClient = await _fixture.CreateClientAsAsync(challenger);
        (await challengerClient.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(300m)))
            .EnsureSuccessStatusCode();

        var notification = await received.Task.WaitAsync(DeliveryTimeout);

        Assert.Equal(leader.Id, notification.BidderId);
        Assert.Equal(305m, notification.Amount);
        Assert.True(notification.Automatic);
        Assert.Null(notification.OutbidBidderId);
    }

    [Fact]
    public async Task An_outbid_leader_is_named_on_the_broadcast()
    {
        var first = await _fixture.CreateUserAsync(UserRole.Bidder);
        var firstClient = await _fixture.CreateClientAsAsync(first);
        (await firstClient.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(150m)))
            .EnsureSuccessStatusCode();

        await _fixture.WaitForOutboxDrainAsync();

        await using var connection = _fixture.CreateHubConnection();
        var received = Capture<BidPlacedNotification>(connection, nameof(IAuctionClient.BidPlaced));

        await connection.StartAsync();
        await connection.InvokeAsync(nameof(AuctionHub.SubscribeToAuction), _auctionId);

        var second = await _fixture.CreateUserAsync(UserRole.Bidder);
        var secondClient = await _fixture.CreateClientAsAsync(second);
        (await secondClient.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(200m)))
            .EnsureSuccessStatusCode();

        var notification = await received.Task.WaitAsync(DeliveryTimeout);

        Assert.Equal(155m, notification.Amount);
        Assert.Equal(StartingPrice, notification.PreviousPrice);
        Assert.Equal(first.Id, notification.OutbidBidderId);
    }

    [Fact]
    public async Task The_bidder_who_lost_the_lead_is_told_even_though_they_joined_no_group()
    {
        var leader = await _fixture.CreateUserAsync(UserRole.Bidder);
        var leaderClient = await _fixture.CreateClientAsAsync(leader);
        (await leaderClient.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(200m)))
            .EnsureSuccessStatusCode();

        await _fixture.WaitForOutboxDrainAsync();

        await using var connection = _fixture.CreateHubConnectionAs(leader);
        var received = Capture<OutbidNotification>(connection, nameof(IAuctionClient.Outbid));
        await connection.StartAsync();

        var rival = await _fixture.CreateUserAsync(UserRole.Bidder);
        var rivalClient = await _fixture.CreateClientAsAsync(rival);
        (await rivalClient.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(500m)))
            .EnsureSuccessStatusCode();

        var notification = await received.Task.WaitAsync(DeliveryTimeout);

        Assert.Equal(_auctionId, notification.AuctionId);
        Assert.Equal("Rare stamp collection", notification.AuctionTitle);
        Assert.Equal(205m, notification.CurrentPrice);
    }

    [Fact]
    public async Task A_leader_whose_proxy_held_the_lot_is_not_told_they_lost_it()
    {
        var leader = await _fixture.CreateUserAsync(UserRole.Bidder);
        var leaderClient = await _fixture.CreateClientAsAsync(leader);
        (await leaderClient.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(500m)))
            .EnsureSuccessStatusCode();

        await _fixture.WaitForOutboxDrainAsync();

        await using var connection = _fixture.CreateHubConnectionAs(leader);
        var received = Capture<OutbidNotification>(connection, nameof(IAuctionClient.Outbid));
        await connection.StartAsync();

        var rival = await _fixture.CreateUserAsync(UserRole.Bidder);
        var rivalClient = await _fixture.CreateClientAsAsync(rival);
        (await rivalClient.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(300m)))
            .EnsureSuccessStatusCode();

        await AssertStaysSilentAsync(received);
    }

    [Fact]
    public async Task Nobody_else_is_told_that_somebody_lost_the_lead()
    {
        var leader = await _fixture.CreateUserAsync(UserRole.Bidder);
        var leaderClient = await _fixture.CreateClientAsAsync(leader);
        (await leaderClient.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(200m)))
            .EnsureSuccessStatusCode();

        await _fixture.WaitForOutboxDrainAsync();

        var onlooker = await _fixture.CreateUserAsync(UserRole.Bidder);
        await using var connection = _fixture.CreateHubConnectionAs(onlooker);
        var received = Capture<OutbidNotification>(connection, nameof(IAuctionClient.Outbid));

        await connection.StartAsync();
        await connection.InvokeAsync(nameof(AuctionHub.SubscribeToAuction), _auctionId);

        var rival = await _fixture.CreateUserAsync(UserRole.Bidder);
        var rivalClient = await _fixture.CreateClientAsAsync(rival);
        (await rivalClient.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(500m)))
            .EnsureSuccessStatusCode();

        await AssertStaysSilentAsync(received);
    }

    [Fact]
    public async Task A_withdrawal_reaches_watchers_and_the_lobby()
    {
        await using var connection = _fixture.CreateHubConnection();
        var received = Capture<AuctionStatusChangedNotification>(
            connection,
            nameof(IAuctionClient.AuctionStatusChanged));

        await connection.StartAsync();
        await connection.InvokeAsync(nameof(AuctionHub.SubscribeToAuction), _auctionId);

        var sellerClient = await _fixture.CreateClientAsAsync(_seller);
        (await sellerClient.PostAsync($"/api/v1/auctions/{_auctionId}/cancel", null))
            .EnsureSuccessStatusCode();

        var notification = await received.Task.WaitAsync(DeliveryTimeout);

        Assert.Equal(_auctionId, notification.AuctionId);
        Assert.Equal(nameof(AuctionStatus.Cancelled), notification.Status);
        Assert.Null(notification.LeadingBidderId);
    }

    [Fact]
    public async Task A_bid_never_reaches_watchers_of_another_auction()
    {
        var otherAuctionId = await CreateAuctionAsync();

        await using var connection = _fixture.CreateHubConnection();
        var received = Capture<BidPlacedNotification>(connection, nameof(IAuctionClient.BidPlaced));

        await connection.StartAsync();
        await connection.InvokeAsync(nameof(AuctionHub.SubscribeToAuction), otherAuctionId);

        var bidder = await _fixture.CreateUserAsync(UserRole.Bidder);
        var client = await _fixture.CreateClientAsAsync(bidder);
        (await client.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(150m)))
            .EnsureSuccessStatusCode();

        await AssertStaysSilentAsync(received);
    }

    [Fact]
    public async Task Unsubscribing_stops_further_broadcasts()
    {
        await using var connection = _fixture.CreateHubConnection();
        var received = Capture<BidPlacedNotification>(connection, nameof(IAuctionClient.BidPlaced));

        await connection.StartAsync();
        await connection.InvokeAsync(nameof(AuctionHub.SubscribeToAuction), _auctionId);
        await connection.InvokeAsync(nameof(AuctionHub.UnsubscribeFromAuction), _auctionId);

        var bidder = await _fixture.CreateUserAsync(UserRole.Bidder);
        var client = await _fixture.CreateClientAsAsync(bidder);
        (await client.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(150m)))
            .EnsureSuccessStatusCode();

        await AssertStaysSilentAsync(received);
    }

    [Fact]
    public async Task Creating_an_auction_announces_its_status_to_the_lobby()
    {
        await using var connection = _fixture.CreateHubConnection();
        var received = Capture<AuctionStatusChangedNotification>(
            connection,
            nameof(IAuctionClient.AuctionStatusChanged));

        await connection.StartAsync();
        await connection.InvokeAsync(nameof(AuctionHub.SubscribeToLobby));

        var sellerClient = await _fixture.CreateClientAsAsync(_seller);
        var now = DateTimeOffset.UtcNow;

        var response = await sellerClient.PostAsJsonAsync(
            "/api/v1/auctions",
            new CreateAuctionRequest(
                "Vintage wristwatch",
                "A detailed description of the lot on offer.",
                StartingPrice,
                Increment,
                now.AddSeconds(-5),
                now.AddDays(3)));

        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<CreateAuctionResponse>(
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(created);

        var notification = await received.Task.WaitAsync(DeliveryTimeout);

        Assert.Equal(created.Id, notification.AuctionId);
        Assert.Equal(nameof(AuctionStatus.Active), notification.Status);
        Assert.Equal(created.Status, notification.Status);
        Assert.Equal(StartingPrice, notification.CurrentPrice);
    }

    [Fact]
    public async Task A_bid_reaches_the_lobby_so_the_salon_list_can_follow_it()
    {
        await using var connection = _fixture.CreateHubConnection();
        var received = Capture<BidPlacedNotification>(connection, nameof(IAuctionClient.BidPlaced));

        await connection.StartAsync();
        await connection.InvokeAsync(nameof(AuctionHub.SubscribeToLobby));

        var bidder = await _fixture.CreateUserAsync(UserRole.Bidder);
        var client = await _fixture.CreateClientAsAsync(bidder);

        (await client.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(150m)))
            .EnsureSuccessStatusCode();

        var notification = await received.Task.WaitAsync(DeliveryTimeout);

        Assert.Equal(_auctionId, notification.AuctionId);
        Assert.Equal(bidder.Id, notification.BidderId);
        Assert.Equal(StartingPrice, notification.Amount);
    }

    [Fact]
    public async Task A_rival_taking_the_lead_reaches_the_lobby_at_the_new_price()
    {
        var first = await _fixture.CreateUserAsync(UserRole.Bidder);
        var firstClient = await _fixture.CreateClientAsAsync(first);
        (await firstClient.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(150m)))
            .EnsureSuccessStatusCode();

        await _fixture.WaitForOutboxDrainAsync();

        await using var connection = _fixture.CreateHubConnection();
        var received = Capture<BidPlacedNotification>(connection, nameof(IAuctionClient.BidPlaced));

        await connection.StartAsync();
        await connection.InvokeAsync(nameof(AuctionHub.SubscribeToLobby));

        var second = await _fixture.CreateUserAsync(UserRole.Bidder);
        var secondClient = await _fixture.CreateClientAsAsync(second);
        (await secondClient.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(200m)))
            .EnsureSuccessStatusCode();

        var notification = await received.Task.WaitAsync(DeliveryTimeout);

        Assert.Equal(_auctionId, notification.AuctionId);
        Assert.Equal(second.Id, notification.BidderId);
        Assert.Equal(155m, notification.Amount);
        Assert.Equal(StartingPrice, notification.PreviousPrice);
    }

    [Fact]
    public async Task Leaving_the_lobby_stops_bid_broadcasts()
    {
        await using var connection = _fixture.CreateHubConnection();
        var received = Capture<BidPlacedNotification>(connection, nameof(IAuctionClient.BidPlaced));

        await connection.StartAsync();
        await connection.InvokeAsync(nameof(AuctionHub.SubscribeToLobby));
        await connection.InvokeAsync(nameof(AuctionHub.UnsubscribeFromLobby));

        var bidder = await _fixture.CreateUserAsync(UserRole.Bidder);
        var client = await _fixture.CreateClientAsAsync(bidder);
        (await client.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(150m)))
            .EnsureSuccessStatusCode();

        await AssertStaysSilentAsync(received);
    }

    [Fact]
    public async Task A_message_published_straight_to_the_broker_is_consumed_and_broadcast()
    {
        await using var connection = _fixture.CreateHubConnection();
        var received = Capture<BidPlacedNotification>(connection, nameof(IAuctionClient.BidPlaced));

        await connection.StartAsync();
        await connection.InvokeAsync(nameof(AuctionHub.SubscribeToAuction), _auctionId);

        var bidId = Guid.CreateVersion7();
        var bidderId = Guid.CreateVersion7();
        var outbidBidderId = Guid.CreateVersion7();
        var occurredAt = DateTimeOffset.UtcNow;

        var bus = _fixture.Services.GetRequiredService<IBus>();

        await bus.Publish(new BidPlacedIntegrationEvent(
            _auctionId,
            "Rare stamp collection",
            bidId,
            bidderId,
            275m,
            false,
            250m,
            outbidBidderId,
            occurredAt.AddDays(1),
            false,
            occurredAt));

        var notification = await received.Task.WaitAsync(DeliveryTimeout);

        Assert.Equal(bidId, notification.BidId);
        Assert.Equal(bidderId, notification.BidderId);
        Assert.Equal(275m, notification.Amount);
        Assert.Equal(250m, notification.PreviousPrice);
        Assert.Equal(outbidBidderId, notification.OutbidBidderId);
    }

    private static TaskCompletionSource<T> Capture<T>(HubConnection connection, string methodName)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<T>(methodName, payload => completion.TrySetResult(payload));

        return completion;
    }

    private static async Task AssertStaysSilentAsync<T>(TaskCompletionSource<T> completion)
    {
        var winner = await Task.WhenAny(completion.Task, Task.Delay(SilenceWindow));

        Assert.NotSame(completion.Task, winner);
    }

    private static string BidsUrl(Guid auctionId) => $"/api/v1/auctions/{auctionId}/bids";

    private Task<Guid> CreateAuctionAsync() =>
        _fixture.ExecuteDbContextAsync(async db =>
        {
            var now = DateTimeOffset.UtcNow;

            var auction = Auction.Create(
                _seller.Id,
                "Rare stamp collection",
                "A detailed description of the lot on offer.",
                StartingPrice,
                Increment,
                now,
                now.AddDays(2),
                now);

            db.Auctions.Add(auction);
            await db.SaveChangesAsync();

            return auction.Id;
        });
}
