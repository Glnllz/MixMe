using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMeShared;
using Microsoft.AspNetCore.SignalR; // Добавлено
using MixMe.Hubs; // Добавлено

namespace MixMe.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ReviewsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext; // Поле для хаба

    // Исправленный конструктор
    public ReviewsController(ApplicationDbContext context, IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    [HttpPost]
    public async Task<IActionResult> LeaveReview([FromBody] Review review)
    {
        // 1. Проверки
        var order = await _context.Orders.FindAsync(review.OrderId);
        if (order == null || order.Status != "Completed")
            return BadRequest("Отзыв можно оставить только после завершения заказа.");

        var existing = await _context.Reviews.AnyAsync(r => r.OrderId == review.OrderId);
        if (existing) return BadRequest("Вы уже оставили отзыв.");

        // 2. Подготовка данных отзыва
        var reviewerProfile = await _context.Profiles.FindAsync(review.ReviewerId);
        review.ReviewerName = reviewerProfile?.Username ?? "Пользователь";
        review.CreatedAt = DateTime.UtcNow;
        review.Status = ModerationStatus.Pending; // Устанавливаем статус "На проверке"

        _context.Reviews.Add(review);

        // --- ЛОГИКА УВЕДОМЛЕНИЙ (Адаптированная) ---

        // 3. Уведомление инженеру (Колокольчик)
        // Мы пишем, что отзыв оставлен, но он появится после проверки
        var notificationText = $"📝 {review.ReviewerName} оставил отзыв. Он появится в профиле после модерации.";
        var notif = new MixMeShared.Notification
        {
            UserId = review.RevieweeId,
            Text = notificationText,
            Link = $"/order/{review.OrderId}", // Ведем в заказ, а не в профиль, так как в профиле его еще нет
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };
        _context.Notifications.Add(notif);

        // 4. Системное сообщение в чат
        _context.Messages.Add(new Message
        {
            OrderId = review.OrderId,
            SenderId = Guid.Empty,
            Content = $"🤖 Заказчик отправил отзыв. Модератор проверит его в течение 24 часов.",
            SentAt = DateTime.UtcNow
        });

        // ВАЖНО: Рейтинг НЕ пересчитываем здесь. Это сделает AdminController при Approve!

        // 5. Сохраняем всё
        await _context.SaveChangesAsync();

        // 6. SignalR (Тост инженеру)
        await _hubContext.Clients.Group(review.RevieweeId.ToString())
            .SendAsync("ReceiveMessage", review.OrderId.ToString(), "🤖", notificationText);
        await _hubContext.Clients.All.SendAsync("UpdateAdminPanel");
        return Ok();
    }

    [HttpGet("engineer/{engineerId}")]
    public async Task<ActionResult<IEnumerable<object>>> GetEngineerReviews(Guid engineerId)
    {
        // Показываем только ОДОБРЕННЫЕ модератором отзывы
        return await _context.Reviews
            .Where(r => r.RevieweeId == engineerId && r.Status == ModerationStatus.Approved)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new {
                r.OrderId,
                r.ReviewerId,
                ReviewerName = _context.Profiles.Where(p => p.Id == r.ReviewerId).Select(p => p.Username).FirstOrDefault(),
                ReviewerAvatarUrl = _context.Profiles.Where(p => p.Id == r.ReviewerId).Select(p => p.AvatarUrl).FirstOrDefault(),
                r.Rating,
                r.Comment,
                r.CreatedAt
            })
            .ToListAsync();
    }
}