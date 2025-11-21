using Microsoft.AspNetCore.Authentication.Cookies;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using PPSNR.Server.Data;
using PPSNR.Server.Hubs;
using PPSNR.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var config = builder.Configuration;

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllers();
builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/octet-stream" });
});
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpsRedirection(options =>
{
    // Ensure http:// redirects to the correct HTTPS port during Development
    if (builder.Environment.IsDevelopment())
    {
        options.HttpsPort = 7084; // matches launchSettings.json
    }
});

// Forwarded headers (when running behind a reverse proxy)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Allow all networks/proxies if explicitly desired (common in dev/docker). Be careful in production.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Database (SQLite by default)
var connectionString = config.GetConnectionString("Default") ?? config["DB_CONNECTION"] ?? "Data Source=app.db";
// Register ONLY the factory to avoid lifetime conflicts, then expose a scoped AppDbContext that resolves via the factory.
// Do NOT register AddDbContext together with AddDbContextFactory as it causes: 
// "Cannot consume scoped service 'DbContextOptions<AppDbContext>' from singleton 'IDbContextFactory<AppDbContext>'".
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

// Auth (Twitch OAuth)
// Only register the Twitch scheme when credentials are provided to avoid
// OAuthOptions.Validate throwing for empty ClientId/ClientSecret in development.
var twitchClientId = config["TWITCH_CLIENT_ID"] ?? config["Authentication:Twitch:ClientId"];
var twitchClientSecret = config["TWITCH_CLIENT_SECRET"] ?? config["Authentication:Twitch:ClientSecret"];

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    // DefaultChallengeScheme is set to Twitch only when credentials are available (see below)
});

authBuilder = authBuilder.AddCookie();

if (!string.IsNullOrWhiteSpace(twitchClientId) && !string.IsNullOrWhiteSpace(twitchClientSecret))
{
    builder.Services.PostConfigureAll<CookieAuthenticationOptions>(o => { }); // no-op, just ensures cookie scheme exists
    authBuilder.AddTwitch("Twitch", options =>
    {
        options.ClientId = twitchClientId;
        options.ClientSecret = twitchClientSecret;
        options.SaveTokens = true;
        options.CallbackPath = "/auth/twitch/callback";

        // Harden the backchannel HTTP client to avoid sporadic TLS/HTTP version issues
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            },
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(15)
        };
        options.Backchannel = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60),
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };

        // Ensure cross-site redirect cookies survive the external roundtrip
        options.CorrelationCookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None;
        options.CorrelationCookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    });

    builder.Services.PostConfigureAll<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(o => { });
    // Update default challenge scheme when Twitch is configured
    builder.Services.Configure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(o =>
    {
        o.DefaultChallengeScheme = "Twitch";
    });
}
// else: run with cookies only; external login will be unavailable until credentials are provided.

builder.Services.AddAuthorization();

// App services
builder.Services.AddSignalR();

var app = builder.Build();

// Apply migrations or create DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // Only enable HSTS in production scenarios
    app.UseHsts();
}

// Respect proxy headers early so scheme and client IP are correct for redirects, links, auth, etc.
app.UseForwardedHeaders();

// Redirect HTTP to HTTPS when possible; note this can't prevent
// clients accidentally speaking HTTP to the HTTPS port, but it will
// ensure proper redirects for HTTP requests to the HTTP port.
app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();
app.UseResponseCompression();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<LayoutHub>("/hubs/layout");
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
