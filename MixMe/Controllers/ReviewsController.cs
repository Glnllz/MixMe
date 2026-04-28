using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMeShared;

namespace MixMe.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        // ВАЖНО: Добавь этот конструктор, чтобы инициализировать _context
        public ReviewsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Получение отзывов инженера
        [HttpGet("engineer/{engineerId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetEngineerReviews(Guid engineerId)
        {
            // Теперь _context не будет null
            var reviews = await _context.Reviews
                .Where(r => r.RevieweeId == engineerId)
                .Select(r => new {
                    r.Id,
                    r.Rating,
                    r.Comment,
                    r.CreatedAt,
                    ReviewerName = _context.Profiles
                        .Where(p => p.Id == r.ReviewerId)
                        .Select(p => p.Username)
                        .FirstOrDefault()
                })
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return Ok(reviews);
        }

        // Добавление отзыва (только если был заказ)
        [HttpPost]
        public async Task<IActionResult> LeaveReview([FromBody] Review review)
        {
            // 1. Ищем завершенный заказ этого пользователя у этого инженера
            // (ServiceId или OrderId должен быть в модели Review)
            var hasCompletedOrder = await _context.Orders
                .AnyAsync(o => o.CustomerId == review.ReviewerId &&
                               o.EngineerId == review.RevieweeId &&
                               o.Status == "Completed"); // Или тот статус, который у тебя означает финал

            if (!hasCompletedOrder)
            {
                return BadRequest("Вы не можете оставить отзыв, так как у вас нет завершенных заказов у этого специалиста.");
            }

            // 2. Проверяем, не оставлял ли он уже отзыв (чтобы не спамить)
            var alreadyReviewed = await _context.Reviews
                .AnyAsync(r => r.ReviewerId == review.ReviewerId && r.OrderId == review.OrderId);

            if (alreadyReviewed)
            {
                return BadRequest("Вы уже оставили отзыв для этого заказа.");
            }

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            // 3. Обновляем средний рейтинг инженера
            var engineer = await _context.Engineers.FindAsync(review.RevieweeId);
            if (engineer != null)
            {
                var ratings = await _context.Reviews
                    .Where(r => r.RevieweeId == review.RevieweeId)
                    .Select(r => r.Rating)
                    .ToListAsync();

                engineer.AvgRating = ratings.Average();
                await _context.SaveChangesAsync();
            }

            return Ok();
        }
    }
}