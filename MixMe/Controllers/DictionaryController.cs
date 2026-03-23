using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMeShared; // Твое пространство имен из Shared проекта

namespace MixMe.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DictionaryController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DictionaryController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("roles")]
    public async Task<ActionResult<IEnumerable<Role>>> GetRoles()
    {
        return await _context.Roles.ToListAsync();
    }

    [HttpGet("genres")]
    public async Task<ActionResult<IEnumerable<Genre>>> GetGenres()
    {
        return await _context.Genres.ToListAsync();
    }
}