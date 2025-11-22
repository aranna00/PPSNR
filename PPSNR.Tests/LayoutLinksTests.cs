using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PPSNR.Server2.Data;
using PPSNR.Server2.Data.Entities;

namespace PPSNR.Tests;

public class LayoutLinksTests
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

        // Seed a StreamerPair into the test DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pair = new StreamerPair { Name = "Test Pair" };
        db.Pairs.Add(pair);
        db.SaveChanges();
    }

    [TearDown]
    public void Teardown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task CreateOrRotateLinks_ReturnsTokensAndSaves()
    {
        // Get the seeded pair
        Guid pairId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            pairId = db.Pairs.Select(p => p.Id).First();
        }

        // Fetch antiforgery token and store cookie
        var tokenResp = await _client.GetAsync("/api/antiforgery/token");
        tokenResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenJson = JsonDocument.Parse(await tokenResp.Content.ReadAsStringAsync());
        tokenJson.RootElement.TryGetProperty("token", out var tokenEl).Should().BeTrue();
        tokenJson.RootElement.TryGetProperty("headerName", out var headerEl).Should().BeTrue();
        var headerName = headerEl.GetString() ?? "RequestVerificationToken";
        var token = tokenEl.GetString();

        // Prepare POST request with antiforgery header
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/pairs/{pairId}/links");
        if (!string.IsNullOrEmpty(token)) request.Headers.Add(headerName, token);
        var resp = await _client.SendAsync(request);

        using var assertionScope = new AssertionScope();
        if (resp.StatusCode != HttpStatusCode.OK)
        {
            var body = await resp.Content.ReadAsStringAsync();
            // Help debugging - fail with server response body
            Assert.Fail($"Expected 200 OK but got {(int)resp.StatusCode} {resp.StatusCode}. Response body: {body}");
        }

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        json.RootElement.TryGetProperty("viewToken", out var viewEl).Should().BeTrue();
        json.RootElement.TryGetProperty("editToken", out var editEl).Should().BeTrue();
        Guid.TryParse(viewEl.GetString(), out var _).Should().BeTrue();
        Guid.TryParse(editEl.GetString(), out var _).Should().BeTrue();

        // Verify DB persisted
        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var link = db2.PairLinks.FirstOrDefault(pl => pl.PairId == pairId);
        link.Should().NotBeNull();

        if (link != null)
        {
            link.ViewToken.Should().NotBeEmpty();
            link.EditToken.Should().NotBeEmpty();
        }
    }
}
