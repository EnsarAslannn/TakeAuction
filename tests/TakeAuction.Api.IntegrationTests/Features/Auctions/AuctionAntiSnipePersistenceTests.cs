using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.IntegrationTests.Common;

namespace TakeAuction.Api.IntegrationTests.Features.Auctions;

[Collection(IntegrationTestCollection.Name)]
public sealed class AuctionAntiSnipePersistenceTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    public AuctionAntiSnipePersistenceTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_lot_with_the_anti_snipe_switched_off_is_stored_switched_off()
    {
        var stored = await RoundTripAsync(window: 0, extension: 0);

        Assert.Equal(0, stored.AntiSnipeWindowSeconds);
        Assert.Equal(0, stored.AntiSnipeExtensionSeconds);
    }

    [Fact]
    public async Task A_lot_keeps_the_anti_snipe_it_was_given()
    {
        var stored = await RoundTripAsync(window: 30, extension: 90);

        Assert.Equal(30, stored.AntiSnipeWindowSeconds);
        Assert.Equal(90, stored.AntiSnipeExtensionSeconds);
    }

    [Fact]
    public async Task A_lot_created_with_the_defaults_is_stored_with_the_defaults()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);
        var now = DateTimeOffset.UtcNow;

        var auction = Auction.Create(seller.Id, "Default lot", "A lot under test.", 100m, 5m, now, now.AddDays(1), now);

        var stored = await SaveAndReloadAsync(auction);

        Assert.Equal(Auction.DefaultAntiSnipeWindowSeconds, stored.AntiSnipeWindowSeconds);
        Assert.Equal(Auction.DefaultAntiSnipeExtensionSeconds, stored.AntiSnipeExtensionSeconds);
    }

    private async Task<Auction> RoundTripAsync(int window, int extension)
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);
        var now = DateTimeOffset.UtcNow;

        var auction = Auction.Create(
            seller.Id,
            "Anti-snipe lot",
            "A lot under test.",
            100m,
            5m,
            now,
            now.AddDays(1),
            now,
            antiSnipeWindowSeconds: window,
            antiSnipeExtensionSeconds: extension);

        return await SaveAndReloadAsync(auction);
    }

    private async Task<Auction> SaveAndReloadAsync(Auction auction)
    {
        await _fixture.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.Auctions.Add(auction);
            return await dbContext.SaveChangesAsync();
        });

        return await _fixture.ExecuteDbContextAsync(dbContext => dbContext.Auctions
            .AsNoTracking()
            .SingleAsync(stored => stored.Id == auction.Id));
    }
}
