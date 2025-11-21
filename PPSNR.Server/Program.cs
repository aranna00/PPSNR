using Microsoft.AspNetCore.Authentication.Cookies;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PPSNR.Server2.Components;
using PPSNR.Server2.Components.Account;
using PPSNR.Server2.Data;
using PPSNR.Server2.Services;

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

builder.Services.AddSignalR();

builder.Services
       .AddRazorComponents()
       .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

// Add MVC controllers for API endpoints under /api (e.g., antiforgery token, layout, images)
builder.Services.AddControllers();

builder.Services
       .AddAuthentication
            (options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
       .AddIdentityCookies();

// Auth (Twitch OAuth)
// Only register Twitch when credentials are present; otherwise run with Identity cookies only.
var twitchClientId = config["TWITCH_CLIENT_ID"] ?? config["Authentication:Twitch:ClientId"];
var twitchClientSecret = config["TWITCH_CLIENT_SECRET"] ?? config["Authentication:Twitch:ClientSecret"];

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

            // Correlation cookie must allow cross-site in external login roundtrip
            options.CorrelationCookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None;
            options.CorrelationCookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
        });

    // Prefer Twitch as the default challenge only when configured.
    builder.Services.Configure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(o =>
    {
        o.DefaultChallengeScheme = "Twitch";
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

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
// Map API controllers so routes like /api/antiforgery/token are available
app.MapControllers();
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();