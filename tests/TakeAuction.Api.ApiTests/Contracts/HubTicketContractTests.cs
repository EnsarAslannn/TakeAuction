using System.Net;
using System.Net.Http.Headers;
using TakeAuction.Api.ApiTests.Common;
using TakeAuction.Api.Features.Auth.GetHubTicket;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class HubTicketContractTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;

    public HubTicketContractTests(ApiTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task An_anonymous_caller_gets_no_ticket()
    {
        using var client = _fixture.CreateRawClient();

        using var response = await client.GetAsync(ApiRoutes.HubTicket);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_cookie_session_can_trade_it_for_a_short_lived_ticket()
    {
        using var session = await _fixture.CreateBidderAsync();

        using var response = await session.GetAsync(ApiRoutes.HubTicket);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ticket = await session.ReadAsync<HubTicketResponse>(response);

        Assert.False(string.IsNullOrWhiteSpace(ticket.Token));
        Assert.True(ticket.ExpiresAtUtc > DateTimeOffset.UtcNow);
        Assert.True(ticket.ExpiresAtUtc < DateTimeOffset.UtcNow.AddMinutes(10));
        Assert.NotEqual(session.AccessToken, ticket.Token);
    }

    [Fact]
    public async Task The_ticket_is_accepted_on_the_hub_but_nowhere_else()
    {
        using var session = await _fixture.CreateBidderAsync();

        using var ticketResponse = await session.GetAsync(ApiRoutes.HubTicket);
        var ticket = await session.ReadAsync<HubTicketResponse>(ticketResponse);

        using var bearer = _fixture.CreateRawClient();
        bearer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ticket.Token);

        using var apiCall = await bearer.GetAsync(ApiRoutes.DiagnosticsInfo);

        Assert.Equal(HttpStatusCode.Unauthorized, apiCall.StatusCode);
    }

    [Fact]
    public async Task An_access_token_is_still_good_for_the_api()
    {
        using var session = await _fixture.CreateBidderAsync();

        using var bearer = session.CreateBearerClient();

        using var apiCall = await bearer.GetAsync(ApiRoutes.DiagnosticsInfo);

        Assert.Equal(HttpStatusCode.OK, apiCall.StatusCode);
    }

    [Fact]
    public async Task A_hub_connection_carrying_the_ticket_is_recognised()
    {
        using var session = await _fixture.CreateBidderAsync();

        using var ticketResponse = await session.GetAsync(ApiRoutes.HubTicket);
        var ticket = await session.ReadAsync<HubTicketResponse>(ticketResponse);

        using var client = _fixture.CreateRawClient();

        using var negotiate = await client.PostAsync(
            $"/hubs/auctions/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(ticket.Token)}",
            content: null);

        Assert.Equal(HttpStatusCode.OK, negotiate.StatusCode);
    }
}
