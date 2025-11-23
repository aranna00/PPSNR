using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace PPSNR.Server2.Controllers;

[ApiController]
[Route("auth")] 
public class AuthController : ControllerBase
{
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        var props = new AuthenticationProperties
        {
            RedirectUri = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl
        };
        return Challenge(props, "Twitch");
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
