using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMe.Hubs;
using MixMeShared;

namespace MixMe.Controllers;
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PostsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext;

    public PostsController(ApplicationDbContext context, IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetPosts([FromQuery] Guid currentUserId)
    {
        // 1. Загружаем посты: только одобренные модератором ИЛИ личные посты пользователя (даже если они еще не проверены)
        var posts = await _context.Posts
// Показываем, если (Одобрено ИЛИ это мой пост), НО только если статус НЕ "Rejected"
.Where(p => (p.Status == ModerationStatus.Approved || p.UserId == currentUserId)
             && p.Status != ModerationStatus.Rejected)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var result = new List<object>();

        // 2. Проходим по списку и собираем метаданные (лайки, аватарки и т.д.)
        foreach (var p in posts)
        {
            // Получаем данные профиля автора поста
            var authorProfile = await _context.Profiles
                .Where(u => u.Id == p.UserId)
                .Select(u => new { u.Username, u.AvatarUrl, u.RoleId })
                .FirstOrDefaultAsync();

            result.Add(new
            {
                p.Id,
                p.UserId,
                p.Content,
                p.ImageUrl,
                p.AudioUrl,
                p.CreatedAt,
                p.Status, // Добавляем статус, чтобы автор видел "Pending"
                LikeCount = await _context.PostLikes.CountAsync(l => l.PostId == p.Id),
                IsLiked = await _context.PostLikes.AnyAsync(l => l.PostId == p.Id && l.UserId == currentUserId),
                Username = authorProfile?.Username ?? "Unknown",
                AvatarUrl = authorProfile?.AvatarUrl,
                RoleId = authorProfile?.RoleId ?? 0
            });
        }

        return Ok(result);
    }

    [HttpPost("{id}/like")]
    public async Task<IActionResult> ToggleLike(int id, [FromQuery] Guid userId)
    {
        var existingLike = await _context.PostLikes.FirstOrDefaultAsync(l => l.PostId == id && l.UserId == userId);
        if (existingLike != null)
        {
            _context.PostLikes.Remove(existingLike); // Убираем лайк, если уже стоит
        }
        else
        {
            _context.PostLikes.Add(new PostLike { PostId = id, UserId = userId });
        }
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePost(int id, [FromQuery] Guid userId)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null) return NotFound();
        if (post.UserId != userId) return Forbid(); // Удалять может только автор

        _context.Posts.Remove(post);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // Создать пост
    [HttpPost]
    public async Task<ActionResult<Post>> CreatePost(Post post)
    {
        post.CreatedAt = DateTime.UtcNow;
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("UpdateAdminPanel");
        return Ok(post);
    }
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<object>>> SearchPosts([FromQuery] string? query)
    {
        var postsQuery = _context.Posts
            .Where(p => _context.Profiles.Any(u => u.Id == p.UserId && u.RoleId == 1)) // Только посты заказчиков
            .OrderByDescending(p => p.CreatedAt)
            .AsQueryable();

        if (!string.IsNullOrEmpty(query))
        {
            postsQuery = postsQuery.Where(p => p.Content.Contains(query));
        }

        return await postsQuery.Select(p => new {
            p.Id,
            p.UserId,
            p.Content,
            p.CreatedAt,
            Username = _context.Profiles.Where(u => u.Id == p.UserId).Select(u => u.Username).FirstOrDefault(),
            AvatarUrl = _context.Profiles.Where(u => u.Id == p.UserId).Select(u => u.AvatarUrl).FirstOrDefault(),
            RoleId = 1
        }).ToListAsync();
    }
}