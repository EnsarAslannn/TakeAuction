using System.Net;
using Microsoft.AspNetCore.Mvc;
using TakeAuction.Api.ApiTests.Common;
using TakeAuction.Api.Features.Auth;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class RefreshSessionContractTests : IAsyncLifetime
{
    private static readonly TimeSpan PastTheRotationGrace = TimeSpan.FromMilliseconds(1200);

    private readonly ApiTestFixture _fixture;

    public RefreshSessionContractTests(ApiTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Signing_in_hands_out_a_refresh_cookie_scoped_to_the_auth_slice()
    {
        using var session = _fixture.CreateSession();

        var response = await session.PostAsync(ApiRoutes.Register, new
        {
            email = ApiTestFixture.UniqueEmail("bidder"),
            displayName = "Refresh Owner",
            password = ApiTestFixture.DefaultPassword,
            role = "Bidder"
        });

        var refreshCookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            cookie => cookie.StartsWith($"{ApiSession.RefreshCookieName}=", StringComparison.Ordinal));

        Assert.Contains("httponly", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(session.RefreshToken);
    }

    [Fact]
    public async Task A_refresh_returns_the_profile_and_a_brand_new_pair()
    {
        using var session = await _fixture.CreateBidderAsync("Refreshing Bidder");

        var before = new
        {
            Access = session.AccessToken,
            Refresh = session.RefreshToken,
            Csrf = session.CsrfToken
        };

        var response = await session.PostAsync(ApiRoutes.Refresh, new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await session.ReadAsync<AuthenticatedUserResponse>(response);

        Assert.Equal(session.UserId, body.Id);
        Assert.Equal("Refreshing Bidder", body.DisplayName);

        Assert.NotEqual(before.Refresh, session.RefreshToken);
        Assert.NotEqual(before.Access, session.AccessToken);
        Assert.NotEqual(before.Csrf, session.CsrfToken);
    }

    [Fact]
    public async Task The_pair_that_comes_back_is_immediately_usable()
    {
        using var session = await _fixture.CreateBidderAsync();

        (await session.PostAsync(ApiRoutes.Refresh, new { })).EnsureSuccessStatusCode();

        var me = await session.GetCurrentUserAsync();

        Assert.NotNull(me);
        Assert.Equal(session.UserId, me.Id);
    }

    [Fact]
    public async Task A_caller_with_no_refresh_cookie_is_turned_away()
    {
        using var client = _fixture.CreateRawClient();

        var response = await client.PostAsync(ApiRoutes.Refresh, content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_token_the_server_never_issued_is_turned_away()
    {
        using var session = await _fixture.CreateBidderAsync();

        var response = await session.RefreshWithTokenAsync("0123456789abcdef0123456789abcdef");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Only_one_of_two_simultaneous_refreshes_rotates_the_token()
    {
        using var session = await _fixture.CreateBidderAsync();
        var presented = session.RefreshToken!;

        var responses = await Task.WhenAll(
            session.RefreshWithTokenAsync(presented),
            session.RefreshWithTokenAsync(presented));

        try
        {
            var winner = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            var loser = Assert.Single(responses, response => response.StatusCode != HttpStatusCode.OK);

            Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);

            var problem = await session.ReadAsync<ProblemDetails>(loser);
            Assert.Equal("Session already refreshed", problem.Title);

            var rotated = ReadCookie(winner, ApiSession.RefreshCookieName);
            Assert.NotNull(rotated);
            Assert.NotEqual(presented, rotated);

            using var afterTheRace = await session.RefreshWithTokenAsync(rotated);

            Assert.Equal(HttpStatusCode.OK, afterTheRace.StatusCode);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task A_losing_refresh_leaves_the_cookies_alone()
    {
        using var session = await _fixture.CreateBidderAsync();
        var presented = session.RefreshToken!;

        (await session.RefreshWithTokenAsync(presented)).EnsureSuccessStatusCode();

        using var loser = await session.RefreshWithTokenAsync(presented);

        Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
        Assert.False(loser.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Replaying_a_rotated_token_burns_the_whole_session()
    {
        using var session = await _fixture.CreateBidderAsync();
        var stolen = session.RefreshToken!;

        (await session.PostAsync(ApiRoutes.Refresh, new { })).EnsureSuccessStatusCode();
        var live = session.RefreshToken!;

        Assert.NotEqual(stolen, live);

        await Task.Delay(PastTheRotationGrace);

        var replay = await session.RefreshWithTokenAsync(stolen);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        var problem = await session.ReadAsync<ProblemDetails>(replay);
        Assert.Equal("Session ended", problem.Title);

        var afterBurn = await session.RefreshWithTokenAsync(live);

        Assert.Equal(HttpStatusCode.Unauthorized, afterBurn.StatusCode);
    }

    [Fact]
    public async Task A_burned_family_cannot_be_revived_by_replaying_further_back()
    {
        using var session = await _fixture.CreateBidderAsync();

        var first = session.RefreshToken!;
        (await session.PostAsync(ApiRoutes.Refresh, new { })).EnsureSuccessStatusCode();
        var second = session.RefreshToken!;
        (await session.PostAsync(ApiRoutes.Refresh, new { })).EnsureSuccessStatusCode();

        await Task.Delay(PastTheRotationGrace);

        (await session.RefreshWithTokenAsync(first)).EnsureUnauthorized();

        foreach (var token in new[] { first, second, session.RefreshToken! })
        {
            var response = await session.RefreshWithTokenAsync(token);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task A_failed_refresh_clears_the_cookies_so_the_client_stops_retrying()
    {
        using var session = await _fixture.CreateBidderAsync();
        var stolen = session.RefreshToken!;

        (await session.PostAsync(ApiRoutes.Refresh, new { })).EnsureSuccessStatusCode();

        await Task.Delay(PastTheRotationGrace);

        var replay = await session.RefreshWithTokenAsync(stolen);
        var cookies = replay.Headers.GetValues("Set-Cookie").ToArray();

        Assert.Contains(cookies, cookie => cookie.StartsWith($"{ApiSession.AccessTokenCookieName}=;", StringComparison.Ordinal));
        Assert.Contains(cookies, cookie => cookie.StartsWith($"{ApiSession.RefreshCookieName}=;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Signing_out_ends_the_session_on_the_server_not_just_in_the_browser()
    {
        using var session = await _fixture.CreateBidderAsync();
        var refreshToken = session.RefreshToken!;

        (await session.LogoutAsync()).EnsureSuccessStatusCode();

        var response = await session.RefreshWithTokenAsync(refreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_rotation_grace_does_not_forgive_a_token_that_was_signed_out()
    {
        using var session = await _fixture.CreateBidderAsync();
        var refreshToken = session.RefreshToken!;

        (await session.LogoutAsync()).EnsureSuccessStatusCode();

        using var response = await session.RefreshWithTokenAsync(refreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task One_signed_out_device_does_not_end_the_other_ones()
    {
        var email = ApiTestFixture.UniqueEmail("bidder");

        using var laptop = _fixture.CreateSession();
        await laptop.RegisterAsync(email, "Two Device Bidder", ApiTestFixture.DefaultPassword, "Bidder");

        using var phone = _fixture.CreateSession();
        (await phone.LoginAsync(email, ApiTestFixture.DefaultPassword)).EnsureSuccessStatusCode();

        (await laptop.LogoutAsync()).EnsureSuccessStatusCode();

        var phoneRefresh = await phone.PostAsync(ApiRoutes.Refresh, new { });

        Assert.Equal(HttpStatusCode.OK, phoneRefresh.StatusCode);
    }

    [Fact]
    public async Task A_cookie_session_still_has_to_present_the_csrf_token_to_refresh()
    {
        using var session = await _fixture.CreateBidderAsync();

        var response = await session.PostAsync(ApiRoutes.Refresh, new { }, withCsrf: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static string? ReadCookie(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        var match = cookies.FirstOrDefault(cookie => cookie.StartsWith($"{name}=", StringComparison.Ordinal));

        if (match is null)
        {
            return null;
        }

        var value = match[(name.Length + 1)..].Split(';')[0];

        return string.IsNullOrEmpty(value) ? null : value;
    }
}

internal static class ResponseAssertions
{
    public static void EnsureUnauthorized(this HttpResponseMessage response) =>
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}
