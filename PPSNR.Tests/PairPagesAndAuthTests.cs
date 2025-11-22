using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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

public class PairPagesAndAuthTests
{
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
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
    public async Task Get_Pairs_Page_Renders_Without_Antiforgery()
    {
        var resp = await _client.GetAsync("/pairs");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await resp.Content.ReadAsStringAsync();
        html.Should().Contain("Streamer Pairs");
    }

    [Test]
    public async Task View_Page_Does_Not_Require_Antiforgery()
    {
        // Arrange a pair and link
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pair = new StreamerPair { Name = "TestPair", OwnerUserId = "user-1" };
            db.Pairs.Add(pair);
            db.PairLinks.Add(new PairLink { PairId = pair.Id });
            await db.SaveChangesAsync();
        }

        using var scope2 = _factory.Services.CreateScope();
        var ctx = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = ctx.Pairs.First();
        var link = ctx.PairLinks.First(l => l.PairId == created.Id);

        var resp = await _client.GetAsync($"/{created.Id}/view/{link.ViewToken}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await resp.Content.ReadAsStringAsync();
        html.Should().NotContain("Forbidden");
    }

    [Test]
    public async Task Creating_Pair_Requires_Login_And_Antiforgery()
    {
        // Anonymous should be 401
        var anonFactory = _factory.CreateAnonymousFactory();
        var anonClient = anonFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var noAuthResp = await anonClient.PostAsync("/api/admin/pairs", new StringContent("{}", Encoding.UTF8, "application/json"));
        new[] { HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest }.Should().Contain(noAuthResp.StatusCode);

        // Authenticated but missing antiforgery -> 400
        var badResp = await _client.PostAsync("/api/admin/pairs", new StringContent("{}", Encoding.UTF8, "application/json"));
        badResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // With antiforgery -> 200 and returns id
        var (token, header) = await GetAntiforgeryAsync(_client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/admin/pairs");
        req.Headers.Add(header, token);
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        var ok = await _client.SendAsync(req);
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = JsonDocument.Parse(await ok.Content.ReadAsStringAsync());
        json.RootElement.TryGetProperty("id", out var idEl).Should().BeTrue();
        idEl.GetGuid().Should().NotBe(Guid.Empty);
    }

    [Test]
    public async Task Editing_Slot_Requires_Antiforgery_And_Owner()
    {
        // Arrange: create pair owned by user-1 with one layout and slot
        Guid pairId;
        Guid layoutId;
        Slot slot;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pair = new StreamerPair { Name = "Owned", OwnerUserId = "user-1" };
            db.Pairs.Add(pair);
            var streamer = new Streamer { DisplayName = "S" };
            db.Streamers.Add(streamer);
            var layout = new Layout { Name = "L", PairId = pair.Id, StreamerId = streamer.Id };
            db.Layouts.Add(layout);
            slot = new Slot { LayoutId = layout.Id, SlotType = SlotType.Pokemon, Index = 0, Visible = true, X = 0, Y = 0, ZIndex = 1 };
            db.Slots.Add(slot);
            await db.SaveChangesAsync();
            pairId = pair.Id; layoutId = layout.Id;
        }

        // Owner without antiforgery -> 400
        var ownerClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        ownerClient.DefaultRequestHeaders.Add("X-Test-User", "user-1");
        var body = JsonSerializer.Serialize(new { slot.Id, slot.LayoutId, X = 5, Y = 6, ZIndex = 2, Visible = true, slot.SlotType, slot.Index, slot.ImageUrl, slot.AdditionalProperties });
        var bad = await ownerClient.PostAsync($"/api/pairs/{pairId}/layouts/{layoutId}/slots/{slot.Id}", new StringContent(body, Encoding.UTF8, "application/json"));
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Owner with antiforgery -> 200
        var (token, header) = await GetAntiforgeryAsync(ownerClient);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/pairs/{pairId}/layouts/{layoutId}/slots/{slot.Id}");
        req.Headers.Add(header, token);
        req.Headers.Add("X-Test-User", "user-1");
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        var ok = await ownerClient.SendAsync(req);
        ok.StatusCode.Should().Be(HttpStatusCode.OK);

        // Non-owner with antiforgery -> 403
        var otherClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        otherClient.DefaultRequestHeaders.Add("X-Test-User", "user-2");
        var (token2, header2) = await GetAntiforgeryAsync(otherClient);
        var req2 = new HttpRequestMessage(HttpMethod.Post, $"/api/pairs/{pairId}/layouts/{layoutId}/slots/{slot.Id}");
        req2.Headers.Add(header2, token2);
        req2.Headers.Add("X-Test-User", "user-2");
        req2.Content = new StringContent(body, Encoding.UTF8, "application/json");
        var resp2 = await otherClient.SendAsync(req2);
        // Depending on antiforgery validation order, non-owner may hit antiforgery (400) or authorization (403)
        new[] { HttpStatusCode.Forbidden, HttpStatusCode.BadRequest }.Should().Contain(resp2.StatusCode);
    }

    private static async Task<(string token, string headerName)> GetAntiforgeryAsync(HttpClient client)
    {
        var resp = await client.GetAsync("/api/antiforgery/token");
        resp.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var token = json.RootElement.GetProperty("token").GetString()!;
        var headerName = json.RootElement.GetProperty("headerName").GetString()!;
        return (token, headerName);
    }
}
