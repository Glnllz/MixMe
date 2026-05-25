using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR; // Добавлено для работы с хабом
using MixMe.Data;
using MixMe.Hubs; // Путь к твоему ChatHub
using MixMeShared;

namespace MixMe.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderFilesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext; // Поле для хаба

    // Исправленный конструктор с внедрением контекста и хаба
    public OrderFilesController(ApplicationDbContext context, IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    [HttpPost]
    public async Task<IActionResult> AddFile(OrderFile file)
    {
        file.UploadedAt = DateTime.UtcNow;
        _context.OrderFiles.Add(file);

        // Формируем текст уведомления для чата
        string fileTypeRu = file.FileType == "Stems" ? "исходники" : "результат";
        string content = $"🤖 Загружены новые {fileTypeRu}: {file.FileName}";

        // Создаем системное сообщение в истории чата
        _context.Messages.Add(new Message
        {
            OrderId = file.OrderId,
            SenderId = Guid.Empty, // Системный ID
            Content = content,
            SentAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        // Отправляем сигнал всем участникам чата заказа через SignalR.
        // Это заставит Blazor-клиент второго пользователя вызвать LoadAllData()
        await _hubContext.Clients.Group(file.OrderId.ToString())
            .SendAsync("ReceiveMessage", file.OrderId.ToString(), "🤖", content);

        return Ok();
    }

    [HttpGet("{orderId}")]
    public async Task<ActionResult<IEnumerable<OrderFile>>> GetFiles(Guid orderId)
    {
        return await _context.OrderFiles
            .Where(f => f.OrderId == orderId)
            .OrderByDescending(f => f.UploadedAt)
            .ToListAsync();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFile(int id)
    {
        var file = await _context.OrderFiles.FindAsync(id);
        if (file == null) return NotFound();

        _context.OrderFiles.Remove(file);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}