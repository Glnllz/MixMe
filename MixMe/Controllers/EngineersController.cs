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
    public async Task<ActionResult<Engineer>> GetEngineer(Guid profileId)
    {
        var engineer = await _context.Engineers
            .Include(e => e.Genres) // Загружаем связанные жанры
            .Include(e => e.Profile) // Загружаем данные из таблицы Profiles
            .FirstOrDefaultAsync(e => e.ProfileId == profileId);

        if (engineer == null) return NotFound();
        return Ok(engineer);
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
    [FromQuery] double? minRating)
    {
        var engineersQuery = _context.Engineers
            .Include(e => e.Profile)
            .Include(e => e.Genres)
            .AsQueryable();

        // Фильтр по имени/биографии
        if (!string.IsNullOrEmpty(query))
        {
            engineersQuery = engineersQuery.Where(e =>
                e.Profile.Username.Contains(query) ||
                e.Bio.Contains(query));
        }

        // Фильтр по жанру
        if (genreId.HasValue)
        {
            engineersQuery = engineersQuery.Where(e => e.Genres.Any(g => g.Id == genreId));
        }

        // Фильтр по рейтингу
        if (minRating.HasValue)
        {
            engineersQuery = engineersQuery.Where(e => e.AvgRating >= minRating.Value);
        }

        var engineers = await engineersQuery.ToListAsync();

        // Формируем результат с минимальной ценой из прайс-листа
        var result = new List<object>();
        foreach (var e in engineers)
        {
            // Ищем минимальную цену услуг этого инженера
            var minServicePrice = await _context.Services
                .Where(s => s.EngineerId == e.ProfileId)
                .OrderBy(s => s.Price)
                .Select(s => s.Price)
                .FirstOrDefaultAsync();

            // Если цена не задана фильтром или проходит по нему
            if (minPrice.HasValue && minServicePrice < minPrice.Value) continue;

            result.Add(new
            {
                e.ProfileId,
                Username = e.Profile.Username,
                AvatarUrl = e.Profile.AvatarUrl,
                e.Bio,
                e.AvgRating,
                e.ExperienceYears,
                Genres = e.Genres.Select(g => g.Name).ToList(),
                MinPrice = minServicePrice
            });
        }

        return Ok(result);
    }
}
