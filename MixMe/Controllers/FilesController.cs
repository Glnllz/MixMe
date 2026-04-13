using Microsoft.AspNetCore.Mvc;

namespace MixMe.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public FilesController(IWebHostEnvironment env) => _env = env;

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("Файл не выбран");

        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
        var path = Path.Combine(_env.WebRootPath, "uploads", fileName);

        using (var stream = new FileStream(path, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Возвращаем путь, по которому файл будет доступен в браузере
        return Ok(new { url = $"/uploads/{fileName}" });
    }
}