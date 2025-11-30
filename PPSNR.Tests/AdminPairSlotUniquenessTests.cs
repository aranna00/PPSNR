using System;
using System.Linq;
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

namespace PPSNR.Tests;

public class AdminPairSlotUniquenessTests
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
    public async Task Creating_New_Pair_Produces_No_Duplicate_Slots()
    {
        // Arrange: fetch antiforgery token
        var (token, header) = await GetAntiforgeryAsync(_client);

        // Act: create sample pair via admin API
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/admin/pairs");
        req.Headers.Add(header, token);
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        json.RootElement.TryGetProperty("id", out var idEl).Should().BeTrue();
        var pairId = idEl.GetGuid();
        pairId.Should().NotBe(Guid.Empty);

        // Assert: query DB and ensure no duplicate slots per layout/type/profile/index
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var layouts = db.Layouts.Where(l => l.PairId == pairId).ToList();
        layouts.Should().NotBeEmpty();

        var layoutIds = layouts.Select(l => l.Id).ToList();
        var slots = db.Slots.Where(s => layoutIds.Contains(s.LayoutId)).ToList();

        // Total expected: per new spec, slots are coupled to layout through SlotPlacement.
        // Each layout now has 6 Pokemon and 8 Badge slots (12/16 per pair).
        var expectedPerLayout = new[] { (SlotType.Pokemon, 6), (SlotType.Badge, 8) };
        var expectedTotal = layouts.Count * expectedPerLayout.Sum(t => t.Item2);
        slots.Count.Should().Be(expectedTotal);

        // Ensure per (LayoutId, SlotType, Profile) the indices are unique and counts match expectations
        foreach (var layout in layouts)
        {
            foreach (var (type, count) in expectedPerLayout)
            {
                var group = slots.Where(s => s.LayoutId == layout.Id && s.SlotType == type).ToList();
                group.Count.Should().Be(count, $"Layout {layout.Id} Type {type} should have {count} slots");
                group.Select(s => s.Index).Distinct().Count().Should().Be(count, $"Indices should be unique for Layout {layout.Id} Type {type}");
            }
        }

        // Strong uniqueness check: no duplicate composite keys (LayoutId, SlotType, Index)
        var dupGroups = slots
            .GroupBy(s => new { s.LayoutId, s.SlotType, s.Index })
            .Where(g => g.Count() > 1)
            .ToList();
        dupGroups.Should().BeEmpty("there must be no duplicate slots by (LayoutId, SlotType, Index)");
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
