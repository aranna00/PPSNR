using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PPSNR.Server.Controllers.Api;

[ApiController]
[Route("api/images")]
public class ImagesController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public ImagesController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpPost("upload/{pairId:guid}/{layoutId:guid}")]
    [Authorize]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Upload(Guid pairId, Guid layoutId, IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file");
        var safeName = string.Join("_", file.FileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var folder = Path.Combine(_env.ContentRootPath, "wwwroot", "resources", pairId.ToString(), layoutId.ToString());
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, safeName);
        await using (var fs = System.IO.File.Create(path))
        {
            await file.CopyToAsync(fs);
        }
        var url = $"/resources/{pairId}/{layoutId}/{safeName}";
        return Ok(new { url });
    }
}
