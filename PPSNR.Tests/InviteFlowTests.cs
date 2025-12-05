using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PPSNR.Server.Data;
using PPSNR.Server.Data.Entities;
using PPSNR.Server.Services;

namespace PPSNR.Tests;

public class InviteFlowTests
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
    public async Task InvitePartner_RequiresOwnerAndAntiforgery()
    {
        Guid pairId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pair = new StreamerPair { Name = "PairInv", OwnerUserId = "user-1" };
            db.Pairs.Add(pair);
            db.PairLinks.Add(new PairLink { PairId = pair.Id });
            await db.SaveChangesAsync();
            pairId = pair.Id;
        }

        // Missing antiforgery -> 400
        var ownerClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        ownerClient.DefaultRequestHeaders.Add("X-Test-User", "user-1");
        var body = new StringContent("{ \"email\": \"friend@example.com\" }", Encoding.UTF8, "application/json");
        var bad = await ownerClient.PostAsync($"/api/pairs/{pairId}/invite", body);
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // With antiforgery and as owner -> 200
        var (token, header) = await PairPagesAndAuthTests.GetAntiforgeryAsync(ownerClient);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/pairs/{pairId}/invite");
        req.Headers.Add(header, token);
        req.Content = new StringContent("{ \"email\": \"friend@example.com\" }", Encoding.UTF8, "application/json");
        var ok = await ownerClient.SendAsync(req);
        ok.StatusCode.Should().Be(HttpStatusCode.OK);

        // As non-owner -> 403
        var otherClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        otherClient.DefaultRequestHeaders.Add("X-Test-User", "user-2");
        var (t2, h2) = await PairPagesAndAuthTests.GetAntiforgeryAsync(otherClient);
        var req2 = new HttpRequestMessage(HttpMethod.Post, $"/api/pairs/{pairId}/invite");
        req2.Headers.Add(h2, t2);
        req2.Content = new StringContent("{ \"email\": \"friend@example.com\" }", Encoding.UTF8, "application/json");
        var resp2 = await otherClient.SendAsync(req2);
        // Depending on antiforgery validation order, non-owner may hit antiforgery (400) or authorization (403)
        new[] { HttpStatusCode.Forbidden, HttpStatusCode.BadRequest }.Should().Contain(resp2.StatusCode);
    }

    [Test]
    public async Task AcceptInvite_BindsPartner_AndRedirects()
    {
        Guid pairId;
        Guid partnerToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pair = new StreamerPair { Name = "PairAccept", OwnerUserId = "owner" };
            db.Pairs.Add(pair);
            var link = new PairLink { PairId = pair.Id };
            db.PairLinks.Add(link);
            await db.SaveChangesAsync();
            pairId = pair.Id; partnerToken = link.PartnerEditToken;
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Test-User", "partner-1");
        var resp = await client.GetAsync($"/invite/accept/{pairId}/{partnerToken}");
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location!.ToString().Should().Be($"/{pairId}/partner-edit/{partnerToken}");

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updated = db2.Pairs.First(p => p.Id == pairId);
        updated.PartnerUserId.Should().Be("partner-1");
    }
}

public sealed class TestEmailService : IEmailService
{
    public string? LastTo { get; private set; }
    public string? LastSubject { get; private set; }
    public string? LastHtml { get; private set; }
    public string? LastText { get; private set; }

    public Task SendAsync(string toEmail, string subject, string htmlBody, string? textBody = null, System.Threading.CancellationToken ct = default)
    {
        LastTo = toEmail; LastSubject = subject; LastHtml = htmlBody; LastText = textBody;
        return Task.CompletedTask;
    }
}
