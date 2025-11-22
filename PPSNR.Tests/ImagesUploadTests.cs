using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace PPSNR.Tests;

public class ImagesUploadTests
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
    public async Task UploadImage_SavesFileAndReturnsUrl()
    {
        var pairId = Guid.NewGuid();
        var layoutId = Guid.NewGuid();

        using var content = new MultipartFormDataContent();
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var byteContent = new ByteArrayContent(bytes);
        byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(byteContent, "file", "test.png");

        var resp = await _client.PostAsync($"/api/images/upload/{pairId}/{layoutId}", content);


        using var scope = new AssertionScope();
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        json.RootElement.TryGetProperty("url", out var urlEl).Should().BeTrue();
        var url = urlEl.GetString();
        url.Should().NotBeNullOrEmpty();

        // Verify file exists in test webroot - search the factory webroot temp folder
        var webRoot = _factory.Services.GetRequiredService<IWebHostEnvironment>().WebRootPath;
        var folder = Path.Combine(webRoot, "resources", pairId.ToString(), layoutId.ToString());
        Directory.Exists(folder).Should().BeTrue();
        Directory.EnumerateFiles(folder).Any().Should().BeTrue();
    }
}
