using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMeShared;

namespace MixMe.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AuthController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost("login")]
    public async Task<ActionResult<UserProfile>> Login([FromBody] LoginDto loginDto)
    {
        // Сначала ищем пользователя по Email
        var user = await _context.Profiles
            .Include(p => p.Role)
            .FirstOrDefaultAsync(u => u.Email == loginDto.Email);

        // Проверяем хэш
        if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.Password))
            return Unauthorized("Неверный email или пароль");
        return Ok(user);
    }

    [HttpPost("register")]
    public async Task<ActionResult<UserProfile>> Register([FromBody] RegisterDto dto)
    {
        if (await _context.Profiles.AnyAsync(u => u.Email == dto.Email))
            return BadRequest("Email уже занят");

        if (await _context.Profiles.AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower()))
            return BadRequest("Имя пользователя уже занято");

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name.ToLower() == dto.Role.ToLower());

        var newUser = new UserProfile
        {
            Username = dto.Username,
            Email = dto.Email,
            Password = BCrypt.Net.BCrypt.HashPassword(dto.Password), // В реальном проекте тут должен быть хэш!
            FullName = dto.FullName,
            RoleId = role?.Id ?? 1
        };

        _context.Profiles.Add(newUser);
        await _context.SaveChangesAsync();

        // Если это инженер, создаем запись в таблице инженеров
        if (dto.Role.ToLower() == "engineer")
        {
            var engineer = new Engineer { ProfileId = newUser.Id, Bio = "Новый специалист" };
            _context.Engineers.Add(engineer);
            await _context.SaveChangesAsync();
        }

        return Ok(newUser);
    }
}