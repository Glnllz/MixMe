using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMeShared;

namespace MixMe.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfilesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProfilesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Метод для получения всех профилей (чтобы проверить)
    [HttpGet("{id}")]
    public async Task<ActionResult<UserProfile>> GetProfile(Guid id)
    {
        // Загружаем профиль вместе с его Ролью
        var profile = await _context.Profiles
            .Include(p => p.Role)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (profile == null)
        {
            return NotFound();
        }

        return Ok(profile);
    }


    [HttpPost]
    public async Task<ActionResult<UserProfile>> CreateProfile(UserProfile profile)
    {
        // Проверяем, существует ли уже такой email
        if (await _context.Profiles.AnyAsync(p => p.Email == profile.Email))
        {
            return BadRequest("Пользователь с таким Email уже существует");
        }

        _context.Profiles.Add(profile);
        await _context.SaveChangesAsync();

        // Если роль "Engineer" (ID = 2, как мы указывали в Seed Data), 
        // создаем пустую запись в таблице инженеров
        if (profile.RoleId == 2)
        {
            var engineerData = new MixMeShared.Engineer // Проверь пространство имен
            {
                ProfileId = profile.Id,
                Bio = "Новый инженер",
                ExperienceYears = 0
            };
            _context.Engineers.Add(engineerData);
            await _context.SaveChangesAsync();
        }

        return Ok(profile);
    }


    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProfile(Guid id, UserProfile updateDto)
    {
        var profile = await _context.Profiles.FindAsync(id);
        if (profile == null) return NotFound();

        if (profile.Username != updateDto.Username)
        {
            bool exists = await _context.Profiles
                .AnyAsync(p => p.Username.ToLower() == updateDto.Username.ToLower() && p.Id != id);
            if (exists) return BadRequest("Это имя пользователя уже занято.");
        }

        profile.Username = updateDto.Username;
        profile.FullName = updateDto.FullName;
        profile.AvatarUrl = updateDto.AvatarUrl;

        // Если пароль пришел не пустой, обновляем его
        if (!string.IsNullOrWhiteSpace(updateDto.Password))
        {
            profile.Password = updateDto.Password; // В идеале тут должен быть BCrypt.HashPassword
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }
}