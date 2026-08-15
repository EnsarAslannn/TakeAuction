using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.IntegrationTests.Common;

namespace TakeAuction.Api.IntegrationTests.Features.Auctions;

[Collection(IntegrationTestCollection.Name)]
public sealed class AuctionSearchIndexTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    private User _seller = null!;

    public AuctionSearchIndexTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _seller = await _fixture.CreateUserAsync(UserRole.Seller);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task The_trigram_extension_is_installed()
    {
        var installed = await ScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_trgm')");

        Assert.True(installed);
    }

    [Fact]
    public async Task The_title_search_index_exists_over_the_lowered_title()
    {
        var definition = await ScalarAsync<string>(
            """SELECT indexdef FROM pg_indexes WHERE indexname = 'IX_auctions_title_trgm'""");

        Assert.Contains("gin", definition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lower", definition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gin_trgm_ops", definition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_planner_reaches_for_it_once_a_scan_would_cost_more()
    {
        await SeedAsync(count: 400);

        // The setting and the EXPLAIN have to share a session, so both go down the same open
        // connection. Forcing the planner's hand is the point: on a table this size a
        // sequential scan is genuinely cheaper, and what is being asked is whether the index
        // can answer at all — a plain B-tree cannot serve a leading wildcard at any price.
        var plan = await _fixture.ExecuteDbContextAsync(async dbContext =>
        {
            await dbContext.Database.OpenConnectionAsync();

            try
            {
                var connection = dbContext.Database.GetDbConnection();

                await using (var settings = connection.CreateCommand())
                {
                    settings.CommandText = "SET enable_seqscan = off";
                    await settings.ExecuteNonQueryAsync();
                }

                await using var explain = connection.CreateCommand();
                explain.CommandText =
                    """
                    EXPLAIN (FORMAT TEXT)
                    SELECT a."Id" FROM auctions AS a WHERE lower(a."Title") LIKE '%stamp%'
                    """;

                // EXPLAIN comes back a row per line, and the index is named further down than
                // the first of them.
                var lines = new List<string>();
                await using var reader = await explain.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    lines.Add(reader.GetString(0));
                }

                return string.Join('\n', lines);
            }
            finally
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        });

        Assert.Contains("IX_auctions_title_trgm", plan, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_still_finds_a_lot_by_any_part_of_its_title_whatever_the_case()
    {
        await SeedAsync(count: 20);

        var client = _fixture.CreateClient();

        var matched = await client.GetFromJsonAsync<PagedAuctions>(
            "/api/v1/auctions?search=STAMP&pageSize=100",
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(matched);
        Assert.Equal(20, matched.TotalCount);
        Assert.All(matched.Items, item =>
            Assert.Contains("stamp", item.Title, StringComparison.OrdinalIgnoreCase));
    }

    private async Task SeedAsync(int count)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;

        for (var index = 0; index < count; index++)
        {
            dbContext.Auctions.Add(Auction.Create(
                _seller.Id,
                $"Rare stamp collection {index}",
                "A detailed description of the lot on offer.",
                100m,
                5m,
                now,
                now.AddDays(2),
                now));
        }

        await dbContext.SaveChangesAsync();
        await ExecuteAsync("ANALYZE auctions");
    }

    private Task ExecuteAsync(string sql) =>
        _fixture.ExecuteDbContextAsync(async dbContext =>
        {
            await dbContext.Database.ExecuteSqlRawAsync(sql);
            return true;
        });

    private Task<T> ScalarAsync<T>(string sql) =>
        _fixture.ExecuteDbContextAsync(async dbContext =>
        {
            await dbContext.Database.OpenConnectionAsync();

            try
            {
                await using var command = dbContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = sql;

                var value = await command.ExecuteScalarAsync();

                return value is null or DBNull ? default! : (T)value;
            }
            finally
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        });

    private sealed record PagedAuctions(IReadOnlyList<AuctionListRow> Items, int TotalCount);

    private sealed record AuctionListRow(Guid Id, string Title);
}
