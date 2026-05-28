using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMeShared;
using Microsoft.AspNetCore.SignalR;
using MixMe.Hubs;

namespace MixMe.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext;
    public AdminController(ApplicationDbContext context, IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    // --- МОДЕРАЦИЯ ПОСТОВ ---
    [HttpGet("posts/pending")]
    public async Task<IActionResult> GetPendingPosts()
    {
        // Вместо Include используем Select, чтобы получить имя автора
        var posts = await _context.Posts
            .Where(p => p.Status == ModerationStatus.Pending)
            .Select(p => new {
                p.Id,
                p.Content,
                p.ImageUrl,
                p.AudioUrl,
                p.CreatedAt,
                AuthorName = _context.Profiles.Where(u => u.Id == p.UserId).Select(u => u.Username).FirstOrDefault()
            })
            .ToListAsync();
        return Ok(posts);
    }

    [HttpPatch("posts/{id}/status")]
    public async Task<IActionResult> UpdatePostStatus(int id, [FromQuery] ModerationStatus status)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null) return NotFound();

        post.Status = status;
        await _context.SaveChangesAsync();

        // ЕСЛИ ПОСТ ОДОБРЕН — отправляем сигнал всем клиентам
        if (status == ModerationStatus.Approved)
        {
            await _hubContext.Clients.All.SendAsync("PostApproved", id);
        }

        return Ok();
    }

    // --- МОДЕРАЦИЯ ОТЗЫВОВ ---
    [HttpGet("reviews/pending")]
    public async Task<IActionResult> GetPendingReviews()
    {
        var reviews = await _context.Reviews
            .Where(r => r.Status == ModerationStatus.Pending)
            .Select(r => new {
                r.Id,
                r.Rating,
                r.Comment,
                r.ReviewerName,
                EngineerName = _context.Profiles.Where(p => p.Id == r.RevieweeId).Select(p => p.Username).FirstOrDefault(),
                r.RevieweeId
            })
            .ToListAsync();
        return Ok(reviews);
    }

    [HttpPatch("reviews/{id}/approve")]
    public async Task<IActionResult> ApproveReview(int id)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null) return NotFound();

        review.Status = ModerationStatus.Approved;

        // ВАЖНО: Пересчитываем рейтинг инженера только после одобрения!
        var engineer = await _context.Engineers.FirstOrDefaultAsync(e => e.ProfileId == review.RevieweeId);
        if (engineer != null)
        {
            var ratings = await _context.Reviews
                .Where(r => r.RevieweeId == review.RevieweeId && r.Status == ModerationStatus.Approved)
                .Select(r => r.Rating)
                .ToListAsync();

            ratings.Add(review.Rating); // Добавляем текущий одобренный
            engineer.AvgRating = ratings.Average();
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("reviews/{id}/reject")]
    public async Task<IActionResult> RejectReview(int id)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null) return NotFound();

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync();
        return Ok();
    }

    // --- АРБИТРАЖ (Спорные заказы) ---
    [HttpGet("orders/disputed")]
    public async Task<IActionResult> GetDisputedOrders()
    {
        return Ok(await _context.Orders
            .Where(o => o.Status == "Disputed")
            .Select(o => new {
                o.Id,
                o.TotalAmount,
                o.ServiceId,
                Customer = _context.Profiles.Where(p => p.Id == o.CustomerId).Select(p => p.Username).FirstOrDefault(),
                Engineer = _context.Profiles.Where(p => p.Id == o.EngineerId).Select(p => p.Username).FirstOrDefault(),
                Files = _context.OrderFiles.Where(f => f.OrderId == o.Id).ToList()
            }).ToListAsync());
    }

    // РЕШЕНИЕ АРБИТРАЖА
    [HttpPatch("orders/{id}/resolve")]
    public async Task<IActionResult> ResolveOrder(Guid id, [FromQuery] string decision)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();

        string content = "";
        if (decision == "Refund")
        {
            order.Status = "Cancelled";
            content = "⚖️ Арбитраж вынес решение: Вернуть средства заказчику. Проект закрыт.";
        }
        else
        {
            order.Status = "Completed";
            order.CompletedAt = DateTime.UtcNow;
            content = "⚖️ Арбитраж вынес решение: Выплатить гонорар инженеру. Проект завершен.";
        }

        // 1. Добавляем системное сообщение в историю чата
        _context.Messages.Add(new Message { OrderId = id, SenderId = Guid.Empty, Content = content, SentAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        // 2. ОТПРАВЛЯЕМ СИГНАЛ В ЧАТ ( SignalR )
        // Это заставит страницу заказа обновиться мгновенно у всех
        await _hubContext.Clients.Group(id.ToString()).SendAsync("ReceiveMessage", id.ToString(), "🤖", content);

        return Ok();
    }
}