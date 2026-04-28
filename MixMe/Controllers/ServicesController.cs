using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMeShared;

namespace MixMe.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ServicesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Получить прайс-лист конкретного инженера
    [HttpGet("engineer/{engineerId}")]
    public async Task<ActionResult<IEnumerable<MusicService>>> GetServices(Guid engineerId)
    {
        return await _context.Services
            .Where(s => s.EngineerId == engineerId)
            .ToListAsync();
    }

    // Добавить новую услугу в прайс
    [HttpPost]
    public async Task<ActionResult<MusicService>> AddService(MusicService service)
    {
        // Привязываем услугу к текущему инженеру
        _context.Services.Add(service);
        await _context.SaveChangesAsync();
        return Ok(service);
    }

    // Удалить услугу
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteService(int id)
    {
        var service = await _context.Services.FindAsync(id);
        if (service == null) return NotFound();

        _context.Services.Remove(service);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}