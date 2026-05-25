using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMeShared;

[Route("api/[controller]")]
[ApiController]
public class ReviewsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public ReviewsController(ApplicationDbContext context) => _context = context;

    // MixMe/Controllers/ReviewsController.cs

    [HttpPost]
    public async Task<IActionResult> LeaveReview([FromBody] Review review)
    {
        var order = await _context.Orders.FindAsync(review.OrderId);
        if (order == null || order.Status != "Completed")
            return BadRequest("Отзыв можно оставить только после завершения заказа.");

        var existing = await _context.Reviews.AnyAsync(r => r.OrderId == review.OrderId);
        if (existing) return BadRequest("Вы уже оставили отзыв.");

        // --- НОВАЯ ЛОГИКА: Находим имя заказчика перед сохранением ---
        var reviewerProfile = await _context.Profiles.FindAsync(review.ReviewerId);
        review.ReviewerName = reviewerProfile?.Username ?? "Пользователь";
        // -------------------------------------------------------------

        review.CreatedAt = DateTime.UtcNow;
        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        // Пересчет рейтинга инженера
        var engineer = await _context.Engineers.FirstOrDefaultAsync(e => e.ProfileId == review.RevieweeId);
        if (engineer != null)
        {
            var allRatings = await _context.Reviews
                .Where(r => r.RevieweeId == review.RevieweeId)
                .Select(r => r.Rating)
                .ToListAsync();

            engineer.AvgRating = allRatings.Average();
            await _context.SaveChangesAsync();
        }

        return Ok();
    }

    // MixMe/Controllers/ReviewsController.cs

    [HttpGet("engineer/{engineerId}")]
    public async Task<ActionResult<IEnumerable<object>>> GetEngineerReviews(Guid engineerId)
    {
        return await _context.Reviews
            .Where(r => r.RevieweeId == engineerId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new {
                OrderId = r.OrderId, // ОБЯЗАТЕЛЬНО ДОБАВЬТЕ ЭТУ СТРОКУ
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