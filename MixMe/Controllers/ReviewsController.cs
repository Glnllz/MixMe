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

        _context.Reviews.Add(review);

        // --- ЛОГИКА УВЕДОМЛЕНИЙ ---

        // 3. Создаем уведомление в БД для инженера (для колокольчика)
        var notificationText = $"⭐ {review.ReviewerName} оставил отзыв ({review.Rating}/5): {review.Comment}";
        var notif = new MixMeShared.Notification
        {
            UserId = review.RevieweeId, // Получатель - инженер
            Text = notificationText,
            Link = $"/profile/{review.RevieweeId}", // Ссылка на страницу с отзывами
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };
        _context.Notifications.Add(notif);

        // 4. Добавляем системное сообщение в чат проекта (для истории)
        _context.Messages.Add(new Message
        {
            OrderId = review.OrderId,
            SenderId = Guid.Empty,
            Content = $"🤖 Заказчик опубликовал отзыв о работе: {review.Rating} звёзд.",
            SentAt = DateTime.UtcNow
        });

        // 5. Пересчет рейтинга инженера
        var engineer = await _context.Engineers.FirstOrDefaultAsync(e => e.ProfileId == review.RevieweeId);
        if (engineer != null)
        {
            var allRatings = await _context.Reviews
                .Where(r => r.RevieweeId == review.RevieweeId)
                .Select(r => r.Rating)
                .ToListAsync();

            // Важно: добавляем текущую оценку в расчет, так как SaveChanges еще не зафиксировал её в списке выше
            allRatings.Add(review.Rating);
            engineer.AvgRating = allRatings.Average();
        }

        // Сохраняем всё одним махом
        await _context.SaveChangesAsync();

        // 6. Отправляем мгновенный сигнал инженеру через SignalR (чтобы выскочил Toast)
        await _hubContext.Clients.Group(review.RevieweeId.ToString())
            .SendAsync("ReceiveMessage", review.OrderId.ToString(), "🤖", notificationText);

        return Ok();
    }

    [HttpGet("engineer/{engineerId}")]
    public async Task<ActionResult<IEnumerable<object>>> GetEngineerReviews(Guid engineerId)
    {
        return await _context.Reviews
            .Where(r => r.RevieweeId == engineerId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new {
                OrderId = r.OrderId,
                ReviewerId = r.ReviewerId,
                ReviewerName = _context.Profiles.Where(p => p.Id == r.ReviewerId).Select(p => p.Username).FirstOrDefault(),
                ReviewerAvatarUrl = _context.Profiles.Where(p => p.Id == r.ReviewerId).Select(p => p.AvatarUrl).FirstOrDefault(),
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();
    }
}