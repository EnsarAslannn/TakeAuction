using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Features.Auctions.CreateAuction;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace TakeAuction.Api.ApiTests.Common;

public sealed class ApiTestFixture : IAsyncLifetime
{
    public const string DefaultPassword = "ApiTests!2026";

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("takeauction_api_tests")
        .WithUsername("takeauction")
        .WithPassword("takeauction_test_pw")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-alpine").Build();

    private ApiTestFactory _factory = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync(), _rabbitMq.StartAsync());

        foreach (var (key, value) in ApiTestFactory.StaticSettings)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", _redis.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__RabbitMq", _rabbitMq.GetConnectionString());

        _factory = new ApiTestFactory();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask(),
            _rabbitMq.DisposeAsync().AsTask());
    }

    public ApiSession CreateSession() => new(this, _factory.CreateClient());

    public HttpClient CreateRawClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    public Task<ApiSession> CreateSellerAsync(string? displayName = null) =>
        CreateSessionAsync(UserRole.Seller, displayName);

    public Task<ApiSession> CreateBidderAsync(string? displayName = null) =>
        CreateSessionAsync(UserRole.Bidder, displayName);

    public async Task<IReadOnlyList<ApiSession>> CreateBiddersAsync(int count)
    {
        var sessions = new List<ApiSession>(count);

        for (var index = 0; index < count; index++)
        {
            sessions.Add(await CreateBidderAsync($"Bidder {index + 1:00}"));
        }

        return sessions;
    }

    public static string UniqueEmail(string prefix) =>
        $"{prefix.ToLowerInvariant()}.{Guid.CreateVersion7():N}@takeauction.test";

    public static object OpenAuctionRequest(
        string title = "Live lot under test",
        decimal startingPrice = 100m,
        decimal minimumBidIncrement = 5m) => new
        {
            title,
            description = "An auction opened by the API contract suite for black-box verification.",
            startingPrice,
            minimumBidIncrement,
            startsAtUtc = DateTimeOffset.UtcNow.AddSeconds(-30),
            endsAtUtc = DateTimeOffset.UtcNow.AddHours(2)
        };

    public static object ScheduledAuctionRequest(string title = "Scheduled lot under test") => new
    {
        title,
        description = "An auction that has not opened yet, used to assert the closed-for-bidding path.",
        startingPrice = 100m,
        minimumBidIncrement = 5m,
        startsAtUtc = DateTimeOffset.UtcNow.AddHours(2),
        endsAtUtc = DateTimeOffset.UtcNow.AddHours(6)
    };

    public async Task<Guid> CreateOpenAuctionAsync(
        ApiSession seller,
        decimal startingPrice = 100m,
        decimal minimumBidIncrement = 5m)
    {
        var response = await seller.PostAsync(
            ApiRoutes.Auctions,
            OpenAuctionRequest(startingPrice: startingPrice, minimumBidIncrement: minimumBidIncrement));

        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<CreateAuctionResponse>(JsonOptions);

        return created!.Id;
    }

    public async Task ResetAsync()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.ExecuteSqlRawAsync(
                """TRUNCATE TABLE "bids", "auctions", "refresh_tokens", "users" RESTART IDENTITY CASCADE;""");
        }

        await _redis.ExecAsync(["redis-cli", "FLUSHALL"]);
    }

    private async Task<ApiSession> CreateSessionAsync(UserRole role, string? displayName)
    {
        var session = CreateSession();

        await session.RegisterAsync(
            UniqueEmail(role.ToString()),
            displayName ?? $"Api {role}",
            DefaultPassword,
            role.ToString());

        return session;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiTestCollection : ICollectionFixture<ApiTestFixture>
{
    public const string Name = "TakeAuction API contract tests";
}
