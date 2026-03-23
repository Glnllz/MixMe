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
}

// Вспомогательный класс (DTO) для обновления
public class EngineerUpdateDto
{
    public string? Bio { get; set; }
    public int ExperienceYears { get; set; }
    public List<int> GenreIds { get; set; } = new();
}