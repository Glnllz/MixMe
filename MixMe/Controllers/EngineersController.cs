using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMeShared;

namespace MixMe.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EngineersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public EngineersController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Получить данные инженера по его ProfileId
    [HttpGet("{profileId}")]
    public async Task<ActionResult<object>> GetEngineer(Guid profileId)
    {
        var engineer = await _context.Engineers
            .Include(e => e.Genres)
            .Include(e => e.Profile)
            .FirstOrDefaultAsync(e => e.ProfileId == profileId);

        if (engineer == null) return NotFound();

        // Считаем количество выполненных заказов
        var completedCount = await _context.Orders
            .CountAsync(o => o.EngineerId == profileId && o.Status == "Completed");

        return Ok(new
        {
            Data = engineer,
            CompletedOrdersCount = completedCount
        });
    }

    // Обновить Bio, опыт и ЖАНРЫ
    [HttpPut("{profileId}")]
    public async Task<IActionResult> UpdateEngineer(Guid profileId, EngineerUpdateDto updateDto)
    {
        var engineer = await _context.Engineers
            .Include(e => e.Genres)
            .FirstOrDefaultAsync(e => e.ProfileId == profileId);

        if (engineer == null) return NotFound();

        engineer.Bio = updateDto.Bio;
        engineer.ExperienceYears = updateDto.ExperienceYears;

        // Обновление жанров (удаляем старые, добавляем новые по ID)
        engineer.Genres.Clear();
        var selectedGenres = await _context.Genres
            .Where(g => updateDto.GenreIds.Contains(g.Id))
            .ToListAsync();

        engineer.Genres.AddRange(selectedGenres);

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("top")]
    public async Task<ActionResult<IEnumerable<object>>> GetTopEngineers()
    {
        var top = await _context.Engineers
            .Include(e => e.Profile)
            .OrderByDescending(e => e.AvgRating)
            .Take(3)
            .Select(e => new {
                e.ProfileId,
                Username = e.Profile.Username,
                AvatarUrl = e.Profile.AvatarUrl,
                AvgRating = e.AvgRating
            })
            .ToListAsync();
        return Ok(top);
    }
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<object>>> SearchEngineers(
    [FromQuery] string? query,
    [FromQuery] int? genreId,
    [FromQuery] decimal? minPrice,
    [FromQuery] decimal? maxPrice, // Добавили макс. цену
    [FromQuery] int? minExperience, // Добавили опыт
    [FromQuery] string? sortBy = "rating") // Сортировка: rating, price_asc, price_desc, experience
    {
        var engineersQuery = _context.Engineers
            .Include(e => e.Profile)
            .Include(e => e.Genres)
            .AsQueryable();

        // Фильтры
        if (!string.IsNullOrEmpty(query)) engineersQuery = engineersQuery.Where(e => e.Profile.Username.Contains(query) || e.Bio.Contains(query));
        if (genreId.HasValue) engineersQuery = engineersQuery.Where(e => e.Genres.Any(g => g.Id == genreId));
        if (minExperience.HasValue) engineersQuery = engineersQuery.Where(e => e.ExperienceYears >= minExperience.Value);

        var engineers = await engineersQuery.ToListAsync();
        var result = new List<dynamic>();

        foreach (var e in engineers)
        {
            var minServicePrice = await _context.Services
                .Where(s => s.EngineerId == e.ProfileId)
                .OrderBy(s => s.Price)
                .Select(s => s.Price)
                .FirstOrDefaultAsync();

            if (minPrice.HasValue && minServicePrice < minPrice.Value) continue;
            if (maxPrice.HasValue && minServicePrice > maxPrice.Value) continue;

            result.Add(new
            {
                e.ProfileId,
                e.Profile.Username,
                e.Profile.AvatarUrl,
                e.Bio,
                e.AvgRating,
                e.ExperienceYears,
                MinPrice = minServicePrice
            });
        }

        // Сортировка в памяти (так как MinPrice вычисляемое)
        return sortBy switch
        {
            "price_asc" => Ok(result.OrderBy(r => r.MinPrice)),
            "price_desc" => Ok(result.OrderByDescending(r => r.MinPrice)),
            "experience" => Ok(result.OrderByDescending(r => r.ExperienceYears)),
            _ => Ok(result.OrderByDescending(r => r.AvgRating))
        };
    }
}
