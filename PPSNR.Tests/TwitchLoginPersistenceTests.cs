using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace PPSNR.Tests;

public class TwitchLoginPersistenceTests
{
    private TwitchAuthWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new TwitchAuthWebApplicationFactory();
        _client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Login_persists_in_application_cookie()
    {
        // Before login -> 401
        var meBefore = await _client.GetAsync("/auth/me");
        meBefore.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Trigger login via explicit external endpoint (fake Twitch handler signs in and redirects to /)
        var login = await _client.GetAsync("/auth/external/Twitch?returnUrl=/");
        login.StatusCode.Should().Be(HttpStatusCode.Redirect);
        login.Headers.Location!.ToString().Should().Be("/");

        // After login -> authenticated
        var meAfter = await _client.GetAsync("/auth/me");
        meAfter.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = JsonDocument.Parse(await meAfter.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("name").GetString().Should().Be("Twitch Test User");
    }

    [Test]
    public async Task Protected_endpoint_does_not_rechallenge_after_login()
    {
        // Initial access to protected endpoint should return 401 (no automatic external challenge)
        var first = await _client.GetAsync("/api/admin/pairs");
        first.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Perform login via explicit external endpoint
        var login = await _client.GetAsync("/auth/external/Twitch?returnUrl=/");
        login.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Now access protected endpoint again -> 200 OK (no re-challenge)
        var second = await _client.GetAsync("/api/admin/pairs");
        second.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
