using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace PPSNR.Tests;

public class AntiforgeryTokenTests
{
    private TestWebApplicationFactory _factory;
    private HttpClient _client;

    [SetUp]
    public void Setup()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    [TearDown]
    public void Teardown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GetAntiforgeryToken_ReturnsTokenAndSetsCookie()
    {
        var resp = await _client.GetAsync("/api/antiforgery/token");

        using var scope = new AssertionScope();

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        json.RootElement.TryGetProperty("token", out var tokenEl).Should().BeTrue();
        json.RootElement.TryGetProperty("headerName", out var headerEl).Should().BeTrue();
        tokenEl.GetString().Should().NotBeNullOrEmpty();

        // Cookie should be set for antiforgery
        var hasCookieHeader = resp.Headers.TryGetValues("Set-Cookie", out var cookieHeaders);
        hasCookieHeader.Should().BeTrue();

        if (hasCookieHeader)
        {
            var headers = cookieHeaders.ToList();
            headers.Should().NotBeNull();
            headers.Any(h => h.Contains(".AspNetCore.Antiforgery")).Should().BeTrue();
        }
    }
}
