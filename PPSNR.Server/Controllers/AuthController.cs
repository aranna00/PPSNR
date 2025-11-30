using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace PPSNR.Server.Controllers;

[ApiController]
[Route("auth")] 
public class AuthController : ControllerBase
{
    [HttpGet("login")]
    public async Task<IActionResult> Login([FromQuery] string? returnUrl = null)
    {
        var targetUrl = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl;

        // If Twitch scheme is registered, challenge it to perform external login
        var schemes = HttpContext.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
        var twitchScheme = await schemes.GetSchemeAsync("Twitch");
        if (twitchScheme != null)
        {
            var props = new AuthenticationProperties { RedirectUri = targetUrl };
            return Challenge(props, "Twitch");
        }

        // Otherwise redirect to local login UI
        var loginUi = $"/Account/Login?ReturnUrl={Uri.EscapeDataString(targetUrl)}";
        return Redirect(loginUi);
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
