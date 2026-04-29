using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMeShared;

namespace MixMe.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderFilesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public OrderFilesController(ApplicationDbContext context) => _context = context;

    [HttpPost]
    public async Task<IActionResult> AddFile(OrderFile file)
    {
        file.UploadedAt = DateTime.UtcNow;
        _context.OrderFiles.Add(file);
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("{orderId}")]
    public async Task<ActionResult<IEnumerable<OrderFile>>> GetFiles(Guid orderId)
    {
        return await _context.OrderFiles
            .Where(f => f.OrderId == orderId)
            .OrderByDescending(f => f.UploadedAt)
            .ToListAsync();
    }
}