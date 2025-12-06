using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using PPSNR.Server.Services;

namespace PPSNR.Server.Controllers.Api;

/// <summary>
/// Example API endpoints for managing external identity links.
/// This demonstrates how to use the ExternalIdentityService and ExternalIdentityProviderRegistry.
/// </summary>
[ApiController]
[Route("api/account")]
[Authorize]
public class ExternalIdentitiesController : ControllerBase
{
    private readonly ExternalIdentityService _externalIdService;
    private readonly ExternalIdentityProviderRegistry _providerRegistry;

    public ExternalIdentitiesController(
        ExternalIdentityService externalIdService,
        ExternalIdentityProviderRegistry providerRegistry)
    {
        _externalIdService = externalIdService;
        _providerRegistry = providerRegistry;
    }

    /// <summary>
    /// Gets all external identities linked to the current user.
    /// </summary>
    [HttpGet("external-identities")]
    public async Task<IActionResult> GetLinkedIdentities()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var identities = await _externalIdService.GetLinkedIdentitiesAsync(userId);
        return Ok(new
        {
            linked = identities.Select(ei => new
            {
                provider = ei.ProviderName,
                displayName = ei.ProviderDisplayName,
                email = ei.ProviderEmail,
                linkedAt = ei.LinkedAt,
                lastVerified = ei.LastVerifiedAt
            })
        });
    }

    /// <summary>
    /// Gets all available external identity providers (configured and not).
    /// </summary>
    [HttpGet("available-providers")]
    [AllowAnonymous]
    public IActionResult GetAvailableProviders()
    {
        var allProviders = _providerRegistry.GetAllProviders();
        var configuredProviders = _providerRegistry.GetConfiguredProviders();

        return Ok(new
        {
            all = allProviders.Select(p => new { name = p.ProviderName, scheme = p.SchemeName }),
            configured = configuredProviders.Select(p => new { name = p.ProviderName, scheme = p.SchemeName })
        });
    }

    /// <summary>
    /// Unlinks an external identity provider from the current user.
    /// </summary>
    [HttpDelete("external-identities/{provider}")]
    public async Task<IActionResult> UnlinkProvider(string provider)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var success = await _externalIdService.UnlinkExternalIdentityAsync(userId, provider);
        if (!success)
            return NotFound(new { error = $"No {provider} identity linked" });

        return Ok(new { message = $"Unlinked {provider} identity" });
    }

    /// <summary>
    /// Example: Initiates linking of an external provider.
    /// In a real app, this would redirect to the provider's auth endpoint.
    /// </summary>
    [HttpPost("link/{provider}")]
    public IActionResult LinkProvider(string provider)
    {
        var providerImpl = _providerRegistry.GetProvider(provider);
        if (providerImpl == null)
            return NotFound(new { error = $"Unknown provider: {provider}" });

        if (!providerImpl.IsConfigured())
            return BadRequest(new { error = $"Provider {provider} is not configured" });

        // In a real implementation, this would:
        // 1. Generate a state parameter
        // 2. Redirect to provider auth endpoint
        // 3. Handle callback and call ExternalIdentityService.LinkExternalIdentityAsync()

        var returnUrl = Url.Action("LinkCallback", new { provider });
        var authUrl = $"/auth/external/{provider}?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}";

        return Ok(new
        {
            message = $"Ready to link {provider}",
            redirectTo = authUrl
        });
    }

    /// <summary>
    /// Callback after external provider auth (example).
    /// In a real app, this would be called from the auth middleware after successful login.
    /// </summary>
    [HttpGet("link/callback")]
    public async Task<IActionResult> LinkCallback(string provider)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var providerImpl = _providerRegistry.GetProvider(provider);
        if (providerImpl == null)
            return NotFound(new { error = $"Unknown provider: {provider}" });

        var providerInfo = await providerImpl.ExtractProviderInfoAsync(HttpContext);
        if (providerInfo == null)
            return BadRequest(new { error = "Could not extract provider info" });

        var linked = await _externalIdService.LinkExternalIdentityAsync(userId, provider, providerInfo);

        return Ok(new
        {
            message = $"Successfully linked {provider}",
            linked = new
            {
                provider = linked.ProviderName,
                displayName = linked.ProviderDisplayName,
                linkedAt = linked.LinkedAt
            }
        });
    }
}

