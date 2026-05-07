using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMeShared;

namespace MixMe.Controllers;
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public MessagesController(ApplicationDbContext context) => _context = context;

    // Сохранить новое сообщение
    [HttpPost]
    public async Task<IActionResult> SaveMessage([FromBody] Message msg)
    {
        msg.SentAt = DateTime.UtcNow;
        _context.Messages.Add(msg);
        await _context.SaveChangesAsync();
        return Ok();
    }

    // Получить историю для конкретного заказа
    [HttpGet("order/{orderId}")]
    public async Task<ActionResult<IEnumerable<Message>>> GetHistory(Guid orderId)
    {
        return await _context.Messages
            .Where(m => m.OrderId == orderId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();
    }
}