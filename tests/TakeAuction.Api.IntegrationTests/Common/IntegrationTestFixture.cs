using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Common.RealTime;
using TakeAuction.Api.Common.Security;
using TakeAuction.Api.Domain.Users;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace TakeAuction.Api.IntegrationTests.Common;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("takeauction_tests")
        .WithUsername("takeauction")
        .WithPassword("takeauction_test_pw")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-alpine").Build();

    private TakeAuctionApiFactory _factory = null!;

    public IServiceProvider Services => _factory.Services;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync(), _rabbitMq.StartAsync());

        foreach (var (key, value) in TakeAuctionApiFactory.StaticSettings)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", _redis.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__RabbitMq", _rabbitMq.GetConnectionString());

        _factory = new TakeAuctionApiFactory();

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

    public HttpClient CreateClient() => _factory.CreateClient();

    public HubConnection CreateHubConnection()
    {
        var server = _factory.Server;

        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, AuctionHub.Route.TrimStart('/')), options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    public async Task<HttpClient> CreateClientAsAsync(User user)
    {
        var client = CreateClient();

        await using var scope = _factory.Services.CreateAsyncScope();
        var tokenGenerator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();
        var token = tokenGenerator.Generate(user);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        return client;
    }

    public async Task<User> CreateUserAsync(UserRole role, string? displayName = null)
    {
        var user = User.Create(
            $"{role}.{Guid.CreateVersion7():N}@takeauction.test",
            displayName ?? $"Demo {role}",
            "not-a-real-hash",
            role);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user;
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

    public async Task<T> ExecuteDbContextAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await action(dbContext);
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
    public const string Name = "TakeAuction integration tests";
}
