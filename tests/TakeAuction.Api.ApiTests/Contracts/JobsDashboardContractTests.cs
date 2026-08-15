using System.Net;
using TakeAuction.Api.ApiTests.Common;
using TakeAuction.Api.Common.Jobs;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class JobsDashboardContractTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;

    public JobsDashboardContractTests(ApiTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task An_anonymous_visitor_is_turned_away()
    {
        using var client = _fixture.CreateRawClient();

        var response = await client.GetAsync(JobsExtensions.DashboardRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Signed in but not an admin, so the answer is Forbidden rather than Unauthorized: they
    // are known, and knowing them is not enough. The dashboard can requeue and delete jobs.
    [Fact]
    public async Task A_signed_in_bidder_is_turned_away()
    {
        using var bidder = await _fixture.CreateBidderAsync();

        var response = await bidder.GetAsync(JobsExtensions.DashboardRoute);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_seller_is_turned_away_too()
    {
        using var seller = await _fixture.CreateSellerAsync("Curious Seller");

        var response = await seller.GetAsync(JobsExtensions.DashboardRoute);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Nobody_can_register_their_way_into_the_role_that_would_open_it()
    {
        using var session = _fixture.CreateSession();

        var response = await session.PostAsync(ApiRoutes.Register, new
        {
            email = $"admin.{Guid.CreateVersion7():N}@takeauction.test",
            displayName = "Would-be Admin",
            password = "Str0ng!Passw0rd",
            role = "Admin"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
