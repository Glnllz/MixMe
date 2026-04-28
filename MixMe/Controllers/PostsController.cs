using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMeShared;

namespace MixMe.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PostsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PostsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Получить все посты с данными пользователей
    // ... существующие импорты ...

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetPosts([FromQuery] Guid currentUserId)
    {
        var posts = await _context.Posts.OrderByDescending(p => p.CreatedAt).ToListAsync();
        var result = new List<object>();

        foreach (var p in posts)
        {
            result.Add(new
            {
                p.Id,
                p.UserId,
                p.Content,
                p.ImageUrl,
                p.AudioUrl,
                p.CreatedAt,
                LikeCount = await _context.PostLikes.CountAsync(l => l.PostId == p.Id),
                IsLiked = await _context.PostLikes.AnyAsync(l => l.PostId == p.Id && l.UserId == currentUserId),
                Username = _context.Profiles.Where(u => u.Id == p.UserId).Select(u => u.Username).FirstOrDefault(),
                AvatarUrl = _context.Profiles.Where(u => u.Id == p.UserId).Select(u => u.AvatarUrl).FirstOrDefault(),
                RoleId = _context.Profiles.Where(u => u.Id == p.UserId).Select(u => u.RoleId).FirstOrDefault()
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