using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MixMe.Data;
using MixMeShared;

namespace MixMe.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config; // 1. Добавляем поле для конфигурации

    // 2. Добавляем IConfiguration в конструктор
    public AuthController(ApplicationDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<ActionResult<object>> Login([FromBody] LoginDto loginDto)
    { 
        var user = await _context.Profiles.Include(p => p.Role)
            .FirstOrDefaultAsync(u => u.Email == loginDto.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.Password))
            return Unauthorized("Неверный email или пароль");
        if (user.IsBanned)
        {
            return BadRequest("Ваш аккаунт заблокирован администратором.");
        }

        var tokenHandler = new JwtSecurityTokenHandler();

        var jwtKey = _config["Jwt:Key"];
        if (string.IsNullOrEmpty(jwtKey)) return StatusCode(500, "JWT Key is not configured");

        var key = Encoding.ASCII.GetBytes(jwtKey);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role?.Name ?? "Customer") 
            }),
            Expires = DateTime.UtcNow.AddDays(7), 
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Issuer = _config["Jwt:Issuer"],
            Audience = _config["Jwt:Audience"]
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return Ok(new
        {
            Token = tokenString,
            User = user
        });
    }

    [HttpPost("register")]
    public async Task<ActionResult<UserProfile>> Register([FromBody] RegisterDto dto)
    {
        if (await _context.Profiles.AnyAsync(u => u.Email == dto.Email))
            return BadRequest("Email уже занят");

        if (await _context.Profiles.AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower()))
            return BadRequest("Имя пользователя уже занято");

        // Ищем роль в базе по имени, пришедшему с фронта
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name.ToLower() == dto.Role.ToLower());

        var newUser = new UserProfile
        {
            Username = dto.Username,
            Email = dto.Email,
            Password = BCrypt.Net.BCrypt.HashPassword(dto.Password), // Хэшируем пароль!
            FullName = dto.FullName,
            RoleId = role?.Id ?? 1 // Если роль не найдена, по умолчанию Customer
        };

        _context.Profiles.Add(newUser);
        await _context.SaveChangesAsync();

        // Если это инженер, создаем пустую запись в таблице инженеров
        if (dto.Role.ToLower() == "engineer")
        {
            var engineer = new Engineer { ProfileId = newUser.Id, Bio = "Новый специалист" };
            _context.Engineers.Add(engineer);
            await _context.SaveChangesAsync();
        }

        return Ok(newUser);
    }
}