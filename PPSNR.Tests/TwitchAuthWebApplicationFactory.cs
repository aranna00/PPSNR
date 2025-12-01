using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Linq;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PPSNR.Server.Data;

namespace PPSNR.Tests;

/// <summary>
/// A test web app factory that keeps the app's real cookie/Identity setup,
/// but replaces the external "Twitch" authentication handler with a fake one
/// that immediately signs-in via the configured DefaultSignInScheme and redirects
/// to the requested returnUrl. This allows testing that the DefaultSignInScheme
/// is correctly configured to the application cookie (so auth persists).
/// </summary>
public class TwitchAuthWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _tempContentRoot;
    private readonly string _tempWebRoot;
    private readonly SqliteConnection _connection;

    public TwitchAuthWebApplicationFactory()
    {
        _tempContentRoot = Path.Combine(Path.GetTempPath(), "ppsnr_twitch_auth_" + Guid.NewGuid());
        _tempWebRoot = Path.Combine(_tempContentRoot, "wwwroot");
        Directory.CreateDirectory(_tempWebRoot);

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
        // Supply required configuration so Program doesn't fail-fast.
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
            // Replace the Twitch scheme handler type with our fake one that signs in immediately.
            services.PostConfigureAll<AuthenticationOptions>(opts =>
            {
                var scheme = opts.Schemes.FirstOrDefault(s => s.Name == "Twitch");
                if (scheme != null)
                {
                    scheme.HandlerType = typeof(FakeTwitchHandler);
                }
            });

            // Use in-memory Sqlite for the EF context factory used by the app; fully isolate provider
            var removeTypes = new[]
            {
                typeof(IDbContextFactory<ApplicationDbContext>),
                typeof(ApplicationDbContext),
                typeof(DbContextOptions<ApplicationDbContext>)
            };
            foreach (var t in removeTypes)
            {
                while (true)
                {
                    var desc = services.FirstOrDefault(d => d.ServiceType == t);
                    if (desc == null) break;
                    services.Remove(desc);
                }
            }

            // Also remove any EF options configurations targeting ApplicationDbContext
            bool removed;
            do
            {
                removed = false;
                var toRemove = services.FirstOrDefault(d =>
                {
                    var st = d.ServiceType;
                    if (st == typeof(DbContextOptions<ApplicationDbContext>)) return true;
                    if (!st.IsGenericType) return false;
                    var def = st.GetGenericTypeDefinition();
                    var args = st.GetGenericArguments();
                    if (args.Any(a => a == typeof(ApplicationDbContext))) return true;
                    if (def == typeof(IOptions<>))
                    {
                        var arg = args[0];
                        if (arg.IsGenericType)
                        {
                            var argDef = arg.GetGenericTypeDefinition();
                            var argArgs = arg.GetGenericArguments();
                            if (argArgs.Any(a => a == typeof(ApplicationDbContext))) return true;
                            if (argDef.Name.Contains("DbContextFactoryOptions") && argArgs.Any(a => a == typeof(ApplicationDbContext))) return true;
                        }
                    }
                    return false;
                });
                if (toRemove != null)
                {
                    services.Remove(toRemove);
                    removed = true;
                }
            } while (removed);

            var efServices = new ServiceCollection();
            efServices.AddEntityFrameworkSqlite();
            var sqliteProvider = efServices.BuildServiceProvider();

            services.AddDbContextFactory<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_connection)
                       .UseInternalServiceProvider(sqliteProvider);
            });
            services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

            // Ensure DB schema exists
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
}

/// <summary>
/// Fake external auth handler for the "Twitch" scheme used in tests.
/// On challenge, it signs in a fake principal using the app's DefaultSignInScheme
/// (so tests validate that it points to the application cookie), and redirects
/// to the requested returnUrl.
/// </summary>
public class FakeTwitchHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public FakeTwitchHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // We don't handle normal authentication here. This scheme is only used for Challenge.
        return Task.FromResult(AuthenticateResult.NoResult());
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // Create a simple principal to sign in
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "twitch-user-1"),
            new Claim(ClaimTypes.Name, "Twitch Test User")
        };
        var identity = new ClaimsIdentity(claims, "FakeTwitch");
        var principal = new ClaimsPrincipal(identity);

        // Use the configured DefaultSignInScheme to persist the principal (this is what we're testing)
        var schemeProvider = Context.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
        var signInScheme = await schemeProvider.GetDefaultSignInSchemeAsync();
        if (signInScheme == null)
        {
            throw new InvalidOperationException("DefaultSignInScheme is not configured.");
        }

        await Context.SignInAsync(signInScheme.Name, principal);

        var redirectUri = properties?.RedirectUri;
        if (string.IsNullOrWhiteSpace(redirectUri)) redirectUri = "/";

        Context.Response.StatusCode = 302;
        Context.Response.Headers.Location = redirectUri;
    }
}
