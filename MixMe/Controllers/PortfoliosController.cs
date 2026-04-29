using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMeShared;

namespace MixMe.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortfoliosController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PortfoliosController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Получить все работы инженера
    [HttpGet("engineer/{engineerId}")]
    public async Task<ActionResult<IEnumerable<PortfolioItem>>> GetPortfolio(Guid engineerId)
    {
        return await _context.Portfolios
            .Where(p => p.EngineerId == engineerId)
            .ToListAsync();
    }

    // Добавить работу в портфолио
    [HttpPost]
    public async Task<ActionResult<PortfolioItem>> AddPortfolioItem(PortfolioItem item)
    {
        _context.Portfolios.Add(item);
        await _context.SaveChangesAsync();
        return Ok(item);
    }
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePortfolioItem(int id)
    {
        var item = await _context.Portfolios.FindAsync(id);
        if (item == null) return NotFound();
        _context.Portfolios.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}