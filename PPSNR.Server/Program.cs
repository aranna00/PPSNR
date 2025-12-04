using System.Net;
using System.Security.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PPSNR.Server.Components;
using PPSNR.Server.Components.Account;
using PPSNR.Server.Data;
using PPSNR.Server.Services;
using PPSNR.Server.Hubs;
using Microsoft.AspNetCore.Hosting.Server;
using IPNetwork = System.Net.IPNetwork;

var builder = WebApplication.CreateBuilder(args);

// Load .env file (simple KEY=VALUE parser) so developers can toggle features locally
var envFile = Path.Combine(AppContext.BaseDirectory, ".env");
if (!File.Exists(envFile))
{
    // also try repository root
    envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
}
if (File.Exists(envFile))
{
    foreach (var line in File.ReadAllLines(envFile))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
        var idx = trimmed.IndexOf('=');
        if (idx <= 0) continue;
        var key = trimmed.Substring(0, idx).Trim();
        var value = trimmed.Substring(idx + 1).Trim();
        // remove optional quotes
        if ((value.StartsWith('\"') && value.EndsWith('\"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
        {
            value = value.Substring(1, value.Length - 2);
        }
        Environment.SetEnvironmentVariable(key, value);
    }
}

// Configuration
var config = builder.Configuration;

// Add services to the container.
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<ImagesCacheService>();
builder.Services.AddScoped<LayoutService>();
builder.Services.AddScoped<AuthService>();
// Register IHttpClientFactory for services that depend on it (e.g., ImagesCacheService)
builder.Services.AddHttpClient();

// Email + Invite services
builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
builder.Services.AddSingleton<IInviteEmailTemplate, DefaultInviteEmailTemplate>();

// Ensure IHttpContextAccessor is available for AuthService
builder.Services.AddHttpContextAccessor();

builder.Services.AddSignalR();

builder.Services
       .AddRazorComponents()
       .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

// Add MVC controllers for API endpoints under /api (e.g., antiforgery token, layout, images)
builder.Services.AddControllers();

// Authentication defaults: Identity cookies are used for sign-in state. Twitch is optional.
// If Twitch credentials are provided, the external scheme will be available, but local accounts
// remain the primary login method.
var twitchClientId = config["TWITCH_CLIENT_ID"] ?? config["Authentication:Twitch:ClientId"];
var twitchClientSecret = config["TWITCH_CLIENT_SECRET"] ?? config["Authentication:Twitch:ClientSecret"];

builder.Services
       .AddAuthentication(options =>
       {
           // Use Identity application cookie as default scheme
           options.DefaultScheme = IdentityConstants.ApplicationScheme;
           // Sign successful external logins into the application cookie
           options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
           // Do not set DefaultChallengeScheme globally; local login UI is the default.
       })
       .AddIdentityCookies();

// Register Twitch only when credentials are supplied
if (!string.IsNullOrWhiteSpace(twitchClientId) && !string.IsNullOrWhiteSpace(twitchClientSecret))
{
    builder.Services
        .AddAuthentication()
        .AddTwitch("Twitch", options =>
        {
            options.ClientId = twitchClientId;
            options.ClientSecret = twitchClientSecret;
            options.SaveTokens = true;
            options.CallbackPath = "/auth/twitch/callback";

            // Use a hardened backchannel client to avoid TLS/HTTP version issues that can
            // surface as TaskCanceledException during code exchange.
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                {
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                },
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                ConnectTimeout = TimeSpan.FromSeconds(15)
            };
            options.Backchannel = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(60),
                DefaultRequestVersion = HttpVersion.Version11,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
            };

            // Correlation cookie must allow cross-site in external login roundtrip
            options.CorrelationCookie.SameSite = SameSiteMode.None;
            options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        });
}

var connectionString = builder.Configuration.GetConnectionString
                           ("DefaultConnection")
                    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Register a DbContextFactory so Razor components/pages can inject
// IDbContextFactory<ApplicationDbContext> (used by Pages/Pair/Index.razor).
// Avoid mixing AddDbContext and AddDbContextFactory for the same context, which
// causes: "Cannot resolve scoped service IEnumerable<IDbContextOptionsConfiguration<ApplicationDbContext>> from root provider".
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Expose a scoped ApplicationDbContext that resolves via the factory.
builder.Services.AddScoped<ApplicationDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services
       .AddIdentityCore<ApplicationUser>
            (options =>
            {
                // Allow immediate sign-in for newly auto-created accounts (including Twitch)
                options.SignIn.RequireConfirmedAccount = false;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
       .AddEntityFrameworkStores<ApplicationDbContext>()
       .AddSignInManager()
       .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Ensure the PostgreSQL database schema is applied at startup (migrations).
// This will create the schema if it doesn't exist and keep it up to date.
using (var scope = app.Services.CreateScope())
{
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("Startup");
    try
    {
        // Skip DB initialization when running under TestServer (unit/integration tests)
        var server = scope.ServiceProvider.GetService<IServer>();
        var serverAsm = server?.GetType().Assembly.GetName().Name;
        var isTestServer = string.Equals(serverAsm, "Microsoft.AspNetCore.TestHost", StringComparison.Ordinal);
        if (isTestServer)
        {
            logger.LogInformation("Detected TestServer; skipping database initialization/migrations.");
            goto AfterDbInit;
        }

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // For PostgreSQL (and most relational providers), apply migrations if available.
        try
        {
            db.Database.Migrate();
        }
        catch (Exception migrateEx)
        {
            // If provider-specific migrations are incompatible (e.g., legacy Sqlite types),
            // fall back to EnsureCreated so fresh databases can be created.
            logger.LogWarning(migrateEx, "Migrate() failed; falling back to EnsureCreated(). Consider regenerating migrations for PostgreSQL.");
            db.Database.EnsureCreated();
        }
    AfterDbInit: { }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error while ensuring database exists and applying migrations.");
        throw;
    }
}

// Always trust reverse-proxy headers from any number of proxies.
// This removes the need for configuration and avoids "Unknown proxy" issues.
var fwdOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                       ForwardedHeaders.XForwardedProto |
                       ForwardedHeaders.XForwardedHost,
    RequireHeaderSymmetry = false,
    ForwardLimit = null
};
// Trust all proxies and networks by clearing the allow-lists
fwdOptions.KnownIPNetworks.Clear();
fwdOptions.KnownProxies.Clear();

app.UseForwardedHeaders(fwdOptions);

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// Allow disabling HTTPS redirection in tests (TestServer doesn't support HTTPS)
if (!app.Configuration.GetValue<bool>("DisableHttpsRedirection"))
{
    app.UseHttpsRedirection();
}


// Serve runtime-generated files under wwwroot (e.g., /resources/* cached images)
app.UseStaticFiles();

app.UseRouting();

// Authentication & Authorization middlewares
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
// Map API controllers so routes like /api/antiforgery/token are available
app.MapControllers();
// Map SignalR hub used by view/edit pages to avoid connection errors during page init
app.MapHub<LayoutHub>("/hubs/layout");
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();
// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();

public partial class Program { }
