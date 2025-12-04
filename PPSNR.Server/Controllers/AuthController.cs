using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace PPSNR.Server.Controllers;

[ApiController]
[Route("auth")] 
public class AuthController : ControllerBase
{
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        // Always redirect to local Identity login UI; from there users can pick external providers (e.g., Twitch)
        var targetUrl = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl;
        var loginUi = $"/Account/Login?ReturnUrl={Uri.EscapeDataString(targetUrl)}";
        return Redirect(loginUi);
    }

    [HttpGet("external/{provider}")]
    public async Task<IActionResult> ExternalLogin(string provider, [FromQuery] string? returnUrl = null)
    {
        var targetUrl = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl;
        var schemes = HttpContext.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await schemes.GetSchemeAsync(provider);
        if (scheme == null)
        {
            return BadRequest(new { error = $"Unknown external provider: {provider}" });
        }
        var props = new AuthenticationProperties { RedirectUri = targetUrl };
        return Challenge(props, provider);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        // Sign out of the application cookie (Identity application scheme)
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return Ok();
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        if (!User.Identity?.IsAuthenticated ?? true) return Unauthorized();
        return Ok(new
        {
            name = User.Identity!.Name,
            claims = User.Claims.Select(c => new { c.Type, c.Value })
        });
    }
}
