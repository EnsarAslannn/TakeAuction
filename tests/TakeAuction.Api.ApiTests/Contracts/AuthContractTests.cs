using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TakeAuction.Api.ApiTests.Common;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Features.Auth;
using TakeAuction.Api.Features.Auth.GetCurrentUser;
using TakeAuction.Api.Features.Auth.Register;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class AuthContractTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;

    public AuthContractTests(ApiTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Register_returns_the_profile_and_points_at_the_session_endpoint()
    {
        using var session = _fixture.CreateSession();
        var email = ApiTestFixture.UniqueEmail("bidder");

        var response = await session.PostAsync(ApiRoutes.Register, new
        {
            email,
            displayName = "Registered Bidder",
            password = ApiTestFixture.DefaultPassword,
            role = nameof(UserRole.Bidder)
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        LocationAssert.PointsAt(ApiRoutes.Me, response.Headers.Location);

        var body = await session.ReadAsync<AuthenticatedUserResponse>(response);

        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal(email, body.Email);
        Assert.Equal("Registered Bidder", body.DisplayName);
        Assert.Equal(nameof(UserRole.Bidder), body.Role);
        Assert.True(body.ExpiresAtUtc > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Register_issues_an_http_only_access_cookie_and_a_readable_csrf_cookie()
    {
        using var session = _fixture.CreateSession();

        var response = await session.PostAsync(ApiRoutes.Register, new
        {
            email = ApiTestFixture.UniqueEmail("bidder"),
            displayName = "Cookie Bidder",
            password = ApiTestFixture.DefaultPassword,
            role = nameof(UserRole.Bidder)
        });

        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();

        var accessCookie = Assert.Single(
            cookies,
            cookie => cookie.StartsWith($"{ApiSession.AccessTokenCookieName}=", StringComparison.Ordinal));
        var csrfCookie = Assert.Single(
            cookies,
            cookie => cookie.StartsWith($"{ApiSession.CsrfCookieName}=", StringComparison.Ordinal));

        Assert.Contains("httponly", accessCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("httponly", csrfCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", accessCookie, StringComparison.OrdinalIgnoreCase);

        Assert.NotNull(session.AccessToken);
        Assert.NotNull(session.CsrfToken);
    }

    [Fact]
    public async Task Register_rejects_a_password_that_fails_the_policy()
    {
        using var session = _fixture.CreateSession();

        var response = await session.PostAsync(ApiRoutes.Register, new
        {
            email = ApiTestFixture.UniqueEmail("bidder"),
            displayName = "Weak Password",
            password = "short",
            role = nameof(UserRole.Bidder)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await session.ReadAsync<ValidationProblemDetails>(response);

        Assert.Contains(nameof(RegisterCommand.Password), problem.Errors.Keys);
    }

    [Fact]
    public async Task Register_rejects_a_role_that_is_not_self_service()
    {
        using var session = _fixture.CreateSession();

        var response = await session.PostAsync(ApiRoutes.Register, new
        {
            email = ApiTestFixture.UniqueEmail("admin"),
            displayName = "Would Be Admin",
            password = ApiTestFixture.DefaultPassword,
            role = nameof(UserRole.Admin)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await session.ReadAsync<ValidationProblemDetails>(response);

        Assert.Contains(nameof(RegisterCommand.Role), problem.Errors.Keys);
    }

    [Fact]
    public async Task Register_reports_a_conflict_for_an_email_that_is_already_taken()
    {
        var email = ApiTestFixture.UniqueEmail("bidder");

        using var first = _fixture.CreateSession();
        await first.RegisterAsync(email, "First Owner", ApiTestFixture.DefaultPassword, nameof(UserRole.Bidder));

        using var second = _fixture.CreateSession();
        var response = await second.PostAsync(ApiRoutes.Register, new
        {
            email,
            displayName = "Second Owner",
            password = ApiTestFixture.DefaultPassword,
            role = nameof(UserRole.Bidder)
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await second.ReadAsync<ProblemDetails>(response);

        Assert.Equal("Email already in use", problem.Title);
    }

    [Fact]
    public async Task Login_returns_the_profile_for_valid_credentials()
    {
        var email = ApiTestFixture.UniqueEmail("bidder");

        using var registration = _fixture.CreateSession();
        await registration.RegisterAsync(email, "Returning Bidder", ApiTestFixture.DefaultPassword, nameof(UserRole.Bidder));

        using var session = _fixture.CreateSession();
        var response = await session.LoginAsync(email, ApiTestFixture.DefaultPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await session.ReadAsync<AuthenticatedUserResponse>(response);

        Assert.Equal(email, body.Email);
        Assert.Equal(nameof(UserRole.Bidder), body.Role);
        Assert.NotNull(session.AccessToken);
    }

    [Fact]
    public async Task Login_rejects_a_wrong_password_without_leaking_which_half_was_wrong()
    {
        var email = ApiTestFixture.UniqueEmail("bidder");

        using var registration = _fixture.CreateSession();
        await registration.RegisterAsync(email, "Guarded Bidder", ApiTestFixture.DefaultPassword, nameof(UserRole.Bidder));

        using var session = _fixture.CreateSession();
        var response = await session.LoginAsync(email, "Wrong!Password9");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var problem = await session.ReadAsync<ProblemDetails>(response);

        Assert.Equal("Invalid credentials", problem.Title);
        Assert.Equal("The email or password is incorrect.", problem.Detail);
    }

    [Fact]
    public async Task Login_rejects_an_unknown_email_with_the_same_shape()
    {
        using var session = _fixture.CreateSession();

        var response = await session.LoginAsync(ApiTestFixture.UniqueEmail("ghost"), ApiTestFixture.DefaultPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var problem = await session.ReadAsync<ProblemDetails>(response);

        Assert.Equal("Invalid credentials", problem.Title);
    }

    [Fact]
    public async Task Login_validates_the_payload_before_touching_the_store()
    {
        using var session = _fixture.CreateSession();

        var response = await session.PostAsync(ApiRoutes.Login, new { email = "", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await session.ReadAsync<ValidationProblemDetails>(response);

        Assert.Contains("Email", problem.Errors.Keys);
        Assert.Contains("Password", problem.Errors.Keys);
    }

    [Fact]
    public async Task Me_reports_no_content_without_a_session()
    {
        using var session = _fixture.CreateSession();

        var response = await session.GetAsync(ApiRoutes.Me);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Me_returns_the_profile_behind_the_session_cookie()
    {
        using var session = await _fixture.CreateBidderAsync("Session Owner");

        var response = await session.GetAsync(ApiRoutes.Me);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await session.ReadAsync<CurrentUserResponse>(response);

        Assert.Equal(session.UserId, body.Id);
        Assert.Equal("Session Owner", body.DisplayName);
        Assert.Equal(nameof(UserRole.Bidder), body.Role);
        Assert.True(body.CreatedAtUtc <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Logout_clears_both_cookies_and_ends_the_session()
    {
        using var session = await _fixture.CreateBidderAsync();

        Assert.NotNull(await session.GetCurrentUserAsync());

        var response = await session.LogoutAsync();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();

        Assert.Contains(cookies, cookie => cookie.StartsWith($"{ApiSession.AccessTokenCookieName}=;", StringComparison.Ordinal));
        Assert.Contains(cookies, cookie => cookie.StartsWith($"{ApiSession.CsrfCookieName}=;", StringComparison.Ordinal));

        Assert.Null(await session.GetCurrentUserAsync());
    }

    [Fact]
    public async Task An_expired_or_forged_token_does_not_authenticate()
    {
        using var client = _fixture.CreateRawClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer not.a.valid.token");

        var response = await client.PostAsJsonAsync(ApiRoutes.Auctions, ApiTestFixture.OpenAuctionRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
