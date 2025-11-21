using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPSNR.Server2.Data;

namespace PPSNR.Server2.Controllers.Api;

[ApiController]
[Route("api/admin")] 
[Authorize]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AdminController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("pairs")] 
    public async Task<IActionResult> GetPairs()
    {
        var pairs = await _db.Pairs.AsNoTracking().ToListAsync();
        var links = await _db.PairLinks.AsNoTracking().ToListAsync();
        var result = pairs.Select(p => new
        {
            p.Id,
            p.Name,
            p.CreatedAt,
            Link = links.FirstOrDefault(l => l.PairId == p.Id)
        });
        return Ok(result);
    }
}
