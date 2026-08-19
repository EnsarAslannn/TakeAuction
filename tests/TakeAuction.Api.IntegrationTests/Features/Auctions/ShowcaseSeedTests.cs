using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Common.Persistence.Seeding;
using TakeAuction.Api.Common.Security;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.IntegrationTests.Common;

namespace TakeAuction.Api.IntegrationTests.Features.Auctions;

[Collection(IntegrationTestCollection.Name)]
public sealed class ShowcaseSeedTests : IAsyncLifetime
{
    private static readonly TimeSpan ShortestShowcaseRun = TimeSpan.FromDays(300);

    private readonly IntegrationTestFixture _fixture;

    private User _seller = null!;

    public ShowcaseSeedTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _seller = await _fixture.CreateUserAsync(UserRole.Seller);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task An_empty_database_gets_showcase_lots_that_stay_open_for_months()
    {
        var now = DateTimeOffset.UtcNow;

        await RunSeederAsync();

        var auctions = await _fixture.ExecuteDbContextAsync(db => db.Auctions
            .AsNoTracking()
            .ToListAsync());

        Assert.Equal(ShowcaseCatalog.Items.Count, auctions.Count);
        Assert.All(auctions, auction => Assert.Equal(AuctionStatus.Active, auction.Status));
        Assert.All(auctions, auction => Assert.True(auction.EndsAtUtc >= now + ShortestShowcaseRun));
    }

    [Fact]
    public async Task A_closed_showcase_lot_reopens_with_its_bidding_history_intact()
    {
        var item = ShowcaseCatalog.Items[0];
        var leader = await _fixture.CreateUserAsync(UserRole.Bidder);
        var auctionId = await CloseShowcaseLotAsync(item, leader.Id);

        var now = DateTimeOffset.UtcNow;

        await RunSeederAsync();

        var auction = await _fixture.ExecuteDbContextAsync(db => db.Auctions
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == auctionId));

        Assert.Equal(AuctionStatus.Active, auction.Status);
        Assert.True(auction.EndsAtUtc >= now + ShortestShowcaseRun);
        Assert.Equal(leader.Id, auction.LeadingBidderId);
        Assert.Equal(item.StartingPrice, auction.CurrentPrice);
        Assert.Equal(1, auction.BidCount);
    }

    [Fact]
    public async Task A_showcase_lot_the_seller_withdrew_stays_withdrawn()
    {
        var item = ShowcaseCatalog.Items[1];
        var auctionId = await CancelShowcaseLotAsync(item);

        await RunSeederAsync();

        var auction = await _fixture.ExecuteDbContextAsync(db => db.Auctions
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == auctionId));

        Assert.Equal(AuctionStatus.Cancelled, auction.Status);
    }

    [Fact]
    public async Task A_showcase_lot_that_is_still_running_keeps_the_closing_time_it_had()
    {
        await RunSeederAsync();

        var before = await ShowcaseEndTimesAsync();

        await RunSeederAsync();

        Assert.Equal(before, await ShowcaseEndTimesAsync());
    }

    [Fact]
    public async Task Seeding_refuses_to_run_without_a_configured_password()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => DatabaseSeeder.SeedAsync(dbContext, new SeedOptions(), passwordHasher, CancellationToken.None));
    }

    private async Task RunSeederAsync()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var options = new SeedOptions { DefaultPassword = "SeedTests!2026" };

        await DatabaseSeeder.SeedAsync(dbContext, options, passwordHasher, CancellationToken.None);
    }

    private Task<Dictionary<Guid, DateTimeOffset>> ShowcaseEndTimesAsync() =>
        _fixture.ExecuteDbContextAsync(db => db.Auctions
            .AsNoTracking()
            .ToDictionaryAsync(auction => auction.Id, auction => auction.EndsAtUtc));

    private async Task<Guid> CloseShowcaseLotAsync(ShowcaseItem item, Guid leaderId)
    {
        var now = DateTimeOffset.UtcNow;
        var startedAt = now.AddDays(-3);
        var auction = NewShowcaseLot(item, startedAt, endsAtUtc: now.AddHours(-1));

        auction.PlaceBid(leaderId, item.StartingPrice, startedAt);
        auction.End(now);

        return await SaveAsync(auction);
    }

    private async Task<Guid> CancelShowcaseLotAsync(ShowcaseItem item)
    {
        var now = DateTimeOffset.UtcNow;
        var auction = NewShowcaseLot(item, now.AddDays(-3), endsAtUtc: now.AddHours(-1));

        auction.Cancel(_seller.Id, now.AddDays(-2));

        return await SaveAsync(auction);
    }

    private Auction NewShowcaseLot(ShowcaseItem item, DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc) =>
        Auction.Create(
            _seller.Id,
            item.Title,
            item.Description,
            item.StartingPrice,
            item.MinimumBidIncrement,
            startsAtUtc,
            endsAtUtc,
            startsAtUtc);

    private async Task<Guid> SaveAsync(Auction auction)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.Auctions.Add(auction);
        await dbContext.SaveChangesAsync();

        return auction.Id;
    }
}
