using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PPSNR.Server2.Data;

namespace PPSNR.Tests;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock systemClock)
        : base(options, logger, encoder, systemClock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Allow tests to set a specific user via header. If header missing, default to test-user.
        var userId = Context.Request.Headers.TryGetValue("X-Test-User", out var vals) && !string.IsNullOrWhiteSpace(vals.ToString())
            ? vals.ToString()
            : "test-user";
        var userName = Context.Request.Headers.TryGetValue("X-Test-Name", out var nameVals) && !string.IsNullOrWhiteSpace(nameVals.ToString())
            ? nameVals.ToString()
            : "Test User";
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Name, userName) };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _tempContentRoot;
    private readonly string _tempWebRoot;
    private readonly SqliteConnection _connection;

    public TestWebApplicationFactory()
    {
        _tempContentRoot = Path.Combine(Path.GetTempPath(), "ppsnr_test_content_" + Guid.NewGuid());
        _tempWebRoot = Path.Combine(_tempContentRoot, "wwwroot");
        Directory.CreateDirectory(_tempWebRoot);

        // Create shared in-memory Sqlite connection
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Ensure Program's fail-fast Twitch config check passes in tests
        Environment.SetEnvironmentVariable("Authentication__Twitch__ClientId", "test-client-id");
        Environment.SetEnvironmentVariable("Authentication__Twitch__ClientSecret", "test-client-secret");
        // Ensure Program's DB connection string is available during startup
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", $"Data Source={Path.Combine(_tempContentRoot, "app.db")} ");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureWebHost(webHostBuilder =>
        {
            webHostBuilder.UseContentRoot(_tempContentRoot);
            webHostBuilder.UseWebRoot(_tempWebRoot);
        });

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Ensure the test host has Twitch credentials (Program requires them at startup).
        builder.ConfigureAppConfiguration((context, config) =>
        {
            var dict = new Dictionary<string, string>
            {
                ["Authentication:Twitch:ClientId"] = "test-client-id",
                ["Authentication:Twitch:ClientSecret"] = "test-client-secret",
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={Path.Combine(_tempContentRoot, "app.db")}"
            };
            config.AddInMemoryCollection(dict);
        });

        builder.ConfigureTestServices(services =>
        {
            // Ensure ISystemClock is available for AuthenticationHandler constructors (older API)
            services.AddSingleton<ISystemClock, SystemClock>();

            // Replace authentication with test scheme
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });

            // Replace EF DbContextFactory/ApplicationDbContext with Sqlite in-memory
            var descriptorFactory = services.FirstOrDefault(d => d.ServiceType == typeof(IDbContextFactory<ApplicationDbContext>));
            if (descriptorFactory != null) services.Remove(descriptorFactory);
            var descriptorCtx = services.FirstOrDefault(d => d.ServiceType == typeof(ApplicationDbContext));
            if (descriptorCtx != null) services.Remove(descriptorCtx);

            services.AddDbContextFactory<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
            services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

            // Ensure DB schema is created
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
        });

        base.ConfigureWebHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (Directory.Exists(_tempContentRoot)) Directory.Delete(_tempContentRoot, true);
        }
        catch { }
        // Clear test env vars
        Environment.SetEnvironmentVariable("Authentication__Twitch__ClientId", null);
        Environment.SetEnvironmentVariable("Authentication__Twitch__ClientSecret", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
        _connection?.Dispose();
        base.Dispose(disposing);
    }

    public WebApplicationFactory<Program> CreateAnonymousFactory()
    {
        // Create a derived factory that disables authentication by default (unauthenticated requests)
        var parent = this;
        return WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services =>
            {
                // Override authentication to a scheme that always fails
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "None";
                    options.DefaultChallengeScheme = "None";
                }).AddScheme<AuthenticationSchemeOptions, RejectAuthHandler>("None", options => { });
            });
        });
    }
}

// Authentication handler that always returns no result (unauthenticated)
public class RejectAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public RejectAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult(AuthenticateResult.NoResult());
}
