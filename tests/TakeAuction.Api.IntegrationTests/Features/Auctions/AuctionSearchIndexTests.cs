using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.IntegrationTests.Common;

namespace TakeAuction.Api.IntegrationTests.Features.Auctions;

[Collection(IntegrationTestCollection.Name)]
public sealed partial class AuctionSearchIndexTests : IAsyncLifetime
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
    public async Task The_search_still_compiles_to_a_pattern_match_the_index_can_answer()
    {
        var sql = await _fixture.ExecuteDbContextAsync(dbContext =>
            Task.FromResult(SearchQuery(dbContext).ToQueryString()));

        Assert.Contains("lower(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" LIKE ", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("strpos(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("position(", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_planner_reaches_for_it_once_a_scan_would_cost_more()
    {
        await SeedAsync(count: 400);

        var plan = await _fixture.ExecuteDbContextAsync(async dbContext =>
        {
            var query = SearchQuery(dbContext).ToQueryString();

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
                explain.CommandText = $"EXPLAIN (FORMAT TEXT)\n{StripParameterComments(query)}";

                foreach (var (name, value) in ReadParameters(query))
                {
                    var parameter = explain.CreateParameter();
                    parameter.ParameterName = name;
                    parameter.Value = value;

                    explain.Parameters.Add(parameter);
                }

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

    private static IQueryable<Guid> SearchQuery(AppDbContext dbContext)
    {
        var pattern = "stamp";

        return dbContext.Auctions
            .AsNoTracking()
            .Where(auction => auction.Title.ToLower().Contains(pattern))
            .Select(auction => auction.Id);
    }

    private static string StripParameterComments(string query) =>
        string.Join(
            '\n',
            query.Split('\n').Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));

    private static IEnumerable<(string Name, string Value)> ReadParameters(string query)
    {
        foreach (Match match in ParameterComment().Matches(query))
        {
            yield return (match.Groups["name"].Value, match.Groups["value"].Value);
        }
    }

    [GeneratedRegex(@"^--\s*@(?<name>\w+)='(?<value>[^']*)'", RegexOptions.Multiline)]
    private static partial Regex ParameterComment();

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
