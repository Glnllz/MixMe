using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMeShared;

namespace MixMe.Controllers;

[Authorize] // Только для залогиненных
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public NotificationsController(ApplicationDbContext context) => _context = context;

    // Получить уведомления пользователя
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<Notification>>> GetUserNotifications(Guid userId)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(20) // Берем последние 20
            .ToListAsync();
    }

    // Пометить как прочитанное
    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var notif = await _context.Notifications.FindAsync(id);
        if (notif == null) return NotFound();

        notif.IsRead = true;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("user/{userId}/clear")]
    public async Task<IActionResult> ClearNotifications(Guid userId)
    {
        var userNotifs = _context.Notifications.Where(n => n.UserId == userId);
        _context.Notifications.RemoveRange(userNotifs);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}