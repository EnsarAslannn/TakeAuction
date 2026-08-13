using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TakeAuction.Api.ApiTests.Common;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class CsrfProtectionContractTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;

    public CsrfProtectionContractTests(ApiTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_cookie_session_that_omits_the_header_is_refused()
    {
        using var seller = await _fixture.CreateSellerAsync();

        var response = await seller.PostAsync(
            ApiRoutes.Auctions,
            ApiTestFixture.OpenAuctionRequest(),
            withCsrf: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var problem = await seller.ReadAsync<ProblemDetails>(response);

        Assert.Equal("CSRF token missing or invalid", problem.Title);
    }

    [Fact]
    public async Task A_cookie_session_that_sends_the_wrong_header_is_refused()
    {
        using var seller = await _fixture.CreateSellerAsync();

        var response = await seller.PostWithCsrfTokenAsync(
            ApiRoutes.Auctions,
            ApiTestFixture.OpenAuctionRequest(),
            csrfToken: "a-token-that-does-not-match-the-cookie");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_cookie_session_that_echoes_the_cookie_is_allowed_through()
    {
        using var seller = await _fixture.CreateSellerAsync();

        Assert.NotNull(seller.CsrfToken);

        var response = await seller.PostWithCsrfTokenAsync(
            ApiRoutes.Auctions,
            ApiTestFixture.OpenAuctionRequest(),
            seller.CsrfToken!);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Safe_reads_never_need_the_token()
    {
        using var seller = await _fixture.CreateSellerAsync();
        await _fixture.CreateOpenAuctionAsync(seller);

        var list = await seller.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Auctions),
            withCsrf: false);

        var me = await seller.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Me),
            withCsrf: false);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task A_bearer_caller_carries_no_cookies_and_is_exempt()
    {
        using var seller = await _fixture.CreateSellerAsync();

        using var bearer = seller.CreateBearerClient(_fixture);

        var response = await bearer.PostAsJsonAsync(ApiRoutes.Auctions, ApiTestFixture.OpenAuctionRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_posts_are_untouched_by_the_double_submit_check()
    {
        using var session = _fixture.CreateSession();

        var response = await session.PostAsync(ApiRoutes.Register, new
        {
            email = ApiTestFixture.UniqueEmail("bidder"),
            displayName = "No Cookie Yet",
            password = ApiTestFixture.DefaultPassword,
            role = "Bidder"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
