using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MixMe.Data;
using MixMeShared;

namespace MixMe.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ComplaintsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public ComplaintsController(ApplicationDbContext context) => _context = context;

    [HttpPost]
    public async Task<IActionResult> Create(Complaint complaint)
    {
        _context.Complaints.Add(complaint);
        await _context.SaveChangesAsync();
        return Ok();
    }
}