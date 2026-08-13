using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TakeAuction.Api.Features.Auth;
using TakeAuction.Api.Features.Auth.GetCurrentUser;

namespace TakeAuction.Api.ApiTests.Common;

/// <summary>
/// Drives the API the way a browser does: an HttpOnly access-token cookie carried by the
/// handler's cookie container, plus the readable CSRF cookie echoed back as a header.
/// </summary>
public sealed class ApiSession : IDisposable
{
    public const string AccessTokenCookieName = "takeauction_access_token";
    public const string CsrfCookieName = "takeauction_csrf";
    public const string CsrfHeaderName = "X-CSRF-TOKEN";

    private readonly HttpClient _client;

    public ApiSession(HttpClient client) => _client = client;

    public string? CsrfToken { get; private set; }

    public string? AccessToken { get; private set; }

    public AuthenticatedUserResponse? User { get; private set; }

    public Guid UserId => User?.Id ?? Guid.Empty;

    public Task<HttpResponseMessage> GetAsync(string url) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, url));

    public Task<HttpResponseMessage> PostAsync<TBody>(string url, TBody body, bool withCsrf = true) =>
        SendAsync(
            new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) },
            withCsrf);

    public Task<HttpResponseMessage> PostWithCsrfTokenAsync<TBody>(string url, TBody body, string csrfToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add(CsrfHeaderName, csrfToken);

        return SendAsync(request, withCsrf: false);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool withCsrf = true)
    {
        if (withCsrf && CsrfToken is not null)
        {
            request.Headers.Add(CsrfHeaderName, CsrfToken);
        }

        var response = await _client.SendAsync(request);
        CaptureCookies(response);

        return response;
    }

    public async Task<AuthenticatedUserResponse> RegisterAsync(
        string email,
        string displayName,
        string password,
        string role)
    {
        var response = await PostAsync(
            ApiRoutes.Register,
            new { email, displayName, password, role });

        response.EnsureSuccessStatusCode();

        User = await ReadJsonAsync<AuthenticatedUserResponse>(response);

        return User!;
    }

    public async Task<HttpResponseMessage> LoginAsync(string email, string password)
    {
        var response = await PostAsync(ApiRoutes.Login, new { email, password });

        if (response.IsSuccessStatusCode)
        {
            User = await ReadJsonAsync<AuthenticatedUserResponse>(response);
        }

        return response;
    }

    public Task<HttpResponseMessage> LogoutAsync() =>
        PostAsync(ApiRoutes.Logout, new { });

    public async Task<CurrentUserResponse?> GetCurrentUserAsync()
    {
        var response = await GetAsync(ApiRoutes.Me);
        response.EnsureSuccessStatusCode();

        return response.StatusCode == System.Net.HttpStatusCode.NoContent
            ? null
            : await ReadJsonAsync<CurrentUserResponse>(response);
    }

    public async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await ReadJsonAsync<T>(response);
        Assert.NotNull(body);

        return body;
    }

    /// <summary>
    /// Deserializes from the string form so a helper and the test that called it can both
    /// read the same response: the content stream is consumed by whoever reads it first.
    /// </summary>
    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), ApiTestFixture.JsonOptions);

    /// <summary>
    /// A client that carries the session's JWT as a bearer header and no cookies at all —
    /// the shape a native or server-to-server caller uses, which is exempt from CSRF.
    /// </summary>
    public HttpClient CreateBearerClient(ApiTestFixture fixture)
    {
        var client = fixture.CreateRawClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);

        return client;
    }

    public void Dispose() => _client.Dispose();

    private void CaptureCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            return;
        }

        foreach (var setCookie in setCookies)
        {
            if (TryReadCookieValue(setCookie, CsrfCookieName, out var csrf))
            {
                CsrfToken = csrf;
            }
            else if (TryReadCookieValue(setCookie, AccessTokenCookieName, out var accessToken))
            {
                AccessToken = accessToken;
            }
        }
    }

    private static bool TryReadCookieValue(string setCookie, string name, out string? value)
    {
        value = null;

        if (!setCookie.StartsWith($"{name}=", StringComparison.Ordinal))
        {
            return false;
        }

        var raw = setCookie[(name.Length + 1)..].Split(';')[0];
        value = string.IsNullOrEmpty(raw) ? null : raw;

        return true;
    }
}

public static class ApiRoutes
{
    public const string Register = "/api/v1/auth/register";
    public const string Login = "/api/v1/auth/login";
    public const string Logout = "/api/v1/auth/logout";
    public const string Me = "/api/v1/auth/me";
    public const string Auctions = "/api/v1/auctions";

    public static string Auction(Guid id) => $"/api/v1/auctions/{id}";

    public static string Bids(Guid id) => $"/api/v1/auctions/{id}/bids";
}

public static class LocationAssert
{
    /// <summary>
    /// Some slices answer with a relative Location and others let routing expand it to an
    /// absolute URL, so assertions compare the tail rather than the whole string.
    /// </summary>
    public static void PointsAt(string expectedPath, Uri? location)
    {
        Assert.NotNull(location);
        Assert.EndsWith(expectedPath, location.ToString(), StringComparison.Ordinal);
    }
}

public static class JsonAssert
{
    public static JsonElement Root(string json) => JsonDocument.Parse(json).RootElement;

    public static void HasProperties(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            Assert.True(
                element.TryGetProperty(name, out _),
                $"expected the payload to expose '{name}' but it was missing from {element}");
        }
    }
}
