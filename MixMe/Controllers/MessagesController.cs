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

        // --- НОВАЯ ЛОГИКА УВЕДОМЛЕНИЙ ---
        // 1. Находим заказ, к которому относится сообщение, чтобы понять, кто участники
        var order = await _context.Orders.FindAsync(msg.OrderId);
        if (order != null)
        {
            // 2. Определяем, кто получает уведомление (если отправил заказчик — получает инженер, и наоборот)
            Guid recipientId = (msg.SenderId == order.CustomerId) ? order.EngineerId : order.CustomerId;

            // 3. Находим имя отправителя для текста уведомления
            var sender = await _context.Profiles.FindAsync(msg.SenderId);
            string senderName = sender?.Username ?? "Партнер";

            // 4. Создаем запись в таблицу уведомлений
            _context.Notifications.Add(new MixMeShared.Notification
            {
                UserId = recipientId,
                Text = $"💬 {senderName}: {msg.Content}", // Текст сообщения в уведомлении
                Link = $"/order/{order.Id}",             // Ссылка сразу в нужный чат
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
        }

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