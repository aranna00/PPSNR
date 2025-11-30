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

var builder = WebApplication.CreateBuilder(args);

// Configuration
var config = builder.Configuration;

// Add services to the container.
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<ImagesCacheService>();
builder.Services.AddScoped<LayoutService>();
builder.Services.AddScoped<AuthService>();
// Register IHttpClientFactory for services that depend on it (e.g., ImagesCacheService)
builder.Services.AddHttpClient();

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

// Consolidate authentication defaults. Twitch is required and is the default challenge provider.
// Identity cookies are still used for application sign-in state.
var twitchClientId = config["Authentication:Twitch:ClientId"];
var twitchClientSecret = config["Authentication:Twitch:ClientSecret"];
if (string.IsNullOrWhiteSpace(twitchClientId) || string.IsNullOrWhiteSpace(twitchClientSecret))
{
    throw new InvalidOperationException("Twitch authentication is required: set Authentication:Twitch:ClientId and Authentication:Twitch:ClientSecret in configuration or environment variables.");
}

builder.Services
       .AddAuthentication(options =>
       {
           // Use Identity application cookie as default scheme, Challenge goes to Twitch
           options.DefaultScheme = IdentityConstants.ApplicationScheme;
           // Sign successful external (Twitch) logins directly into the application cookie
           // so the auth state persists across requests and we don't keep challenging.
           options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
           options.DefaultChallengeScheme = "Twitch";
       })
       .AddIdentityCookies();

// Register Twitch unconditionally (we failed-fast above if keys were missing)
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

var connectionString = builder.Configuration.GetConnectionString
                           ("DefaultConnection")
                    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Register a DbContextFactory so Razor components/pages can inject
// IDbContextFactory<ApplicationDbContext> (used by Pages/Pair/Index.razor).
// Avoid mixing AddDbContext and AddDbContextFactory for the same context, which
// causes: "Cannot resolve scoped service IEnumerable<IDbContextOptionsConfiguration<ApplicationDbContext>> from root provider".
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// Expose a scoped ApplicationDbContext that resolves via the factory.
builder.Services.AddScoped<ApplicationDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services
       .AddIdentityCore<ApplicationUser>
            (options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
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

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                       ForwardedHeaders.XForwardedProto |
                       ForwardedHeaders.XForwardedHost,
    KnownProxies = { IPAddress.Parse("192.168.178.21") },
});

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
