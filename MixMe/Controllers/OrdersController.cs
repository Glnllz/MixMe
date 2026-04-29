using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMeShared;
using MixMe.Services;
using Yandex.Checkout.V3;
using Microsoft.AspNetCore.SignalR; // Добавлено
using MixMe.Hubs; // Добавлено

namespace MixMe.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext; // Добавлено

    public OrdersController(ApplicationDbContext context, IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    [HttpPost("create")]
    public async Task<ActionResult<Guid>> CreateOrder(CreateOrderDto dto)
    {
        var newOrder = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = dto.CustomerId,
            EngineerId = dto.EngineerId,
            ServiceId = dto.ServiceId,
            TotalAmount = dto.TotalAmount,
            Status = "WaitingForPayment",
            CreatedAt = DateTime.UtcNow
        };

        _context.Orders.Add(newOrder);
        await _context.SaveChangesAsync();

        var service = await _context.Services.FindAsync(dto.ServiceId);
        string serviceName = service?.Name ?? "Индивидуальное предложение";

        var systemMsg = new Message
        {
            OrderId = newOrder.Id,
            SenderId = Guid.Empty,
            Content = $"🤖 Проект '{serviceName}' создан. Ожидается оплата заказчиком.",
            SentAt = DateTime.UtcNow
        };

        _context.Messages.Add(systemMsg);
        await _context.SaveChangesAsync();

        return Ok(newOrder.Id);
    }

    // ... методы GetUserOrders и GetOrderDetails без изменений ...
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<object>>> GetUserOrders(Guid userId, [FromQuery] Guid requesterId)
    {
        if (userId != requesterId) return Forbid();

        return Ok(await _context.Orders
            .Where(o => o.CustomerId == userId || o.EngineerId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new {
                Id = o.Id,
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                PartnerName = _context.Profiles.Where(p => p.Id == (o.CustomerId == userId ? o.EngineerId : o.CustomerId)).Select(p => p.Username).FirstOrDefault(),
                PartnerAvatar = _context.Profiles.Where(p => p.Id == (o.CustomerId == userId ? o.EngineerId : o.CustomerId)).Select(p => p.AvatarUrl).FirstOrDefault(),
                LastMessage = _context.Messages.Where(m => m.OrderId == o.Id).OrderByDescending(m => m.SentAt).Select(m => m.Content).FirstOrDefault() ?? "Чат создан",
                ServiceName = _context.Services.Where(s => s.Id == o.ServiceId).Select(s => s.Name).FirstOrDefault() ?? "Индивидуальный заказ"
            }).ToListAsync());
    }


    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetOrderDetails(Guid id, [FromQuery] Guid currentUserId)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();

        // Проверка безопасности: только участники заказа могут видеть детали
        if (order.CustomerId != currentUserId && order.EngineerId != currentUserId)
            return StatusCode(403);

        var customerName = await _context.Profiles
            .Where(p => p.Id == order.CustomerId)
            .Select(p => p.Username)
            .FirstOrDefaultAsync();

        var engineerName = await _context.Profiles
            .Where(p => p.Id == order.EngineerId)
            .Select(p => p.Username)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            Id = order.Id,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            // Обязательно передаем ID участников для работы системы отзывов и файлов
            CustomerId = order.CustomerId,
            EngineerId = order.EngineerId,
            PartnerName = (currentUserId == order.CustomerId) ? engineerName : customerName,
            ServiceName = _context.Services
                .Where(s => s.Id == order.ServiceId)
                .Select(s => s.Name)
                .FirstOrDefault() ?? "Индивидуальный заказ",
            PaymentId = order.PaymentId
        });
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromQuery] string newStatus, [FromQuery] Guid currentUserId)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();

        order.Status = newStatus;
        string content = $"🤖 Статус заказа изменен на: {TranslateStatusForChat(newStatus)}";

        _context.Messages.Add(new Message { OrderId = id, SenderId = Guid.Empty, Content = content, SentAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        // УВЕДОМЛЕНИЕ В РЕАЛЬНОМ ВРЕМЕНИ
        await _hubContext.Clients.Group(id.ToString()).SendAsync("ReceiveMessage", id.ToString(), "Система", content);

        return Ok();
    }

    [HttpPost("{id}/pay")]
    public async Task<ActionResult<string>> CreatePayment(Guid id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();
        var paymentService = HttpContext.RequestServices.GetRequiredService<PaymentService>();
        var payment = await paymentService.CreateOrderPayment(order, $"https://localhost:7246/order/{id}");
        order.PaymentId = payment.Id;
        await _context.SaveChangesAsync();
        return Ok(payment.Confirmation.ConfirmationUrl);
    }

    [HttpGet("{id}/check-status")]
    public async Task<IActionResult> CheckPaymentStatus(Guid id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null || string.IsNullOrEmpty(order.PaymentId)) return NotFound();

        var paymentService = HttpContext.RequestServices.GetRequiredService<PaymentService>();
        var payment = await paymentService.GetPaymentStatus(order.PaymentId);

        bool isPaid = payment.Status == Yandex.Checkout.V3.PaymentStatus.Succeeded ||
                      payment.Status == Yandex.Checkout.V3.PaymentStatus.WaitingForCapture;

        if (isPaid && order.Status == "WaitingForPayment")
        {
            order.Status = "InProgress";
            string content = "✅ Оплата подтверждена. Средства зарезервированы. Исполнитель, приступайте к работе!";

            _context.Messages.Add(new Message { OrderId = order.Id, SenderId = Guid.Empty, Content = content, SentAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            // УВЕДОМЛЕНИЕ В РЕАЛЬНОМ ВРЕМЕНИ
            await _hubContext.Clients.Group(id.ToString()).SendAsync("ReceiveMessage", id.ToString(), "Система", content);

            return Ok(new { paid = true });
        }
        return Ok(new { paid = false });
    }

    private string TranslateStatusForChat(string status) => status switch
    {
        "InProgress" => "В работе",
        "UnderReview" => "На проверке (результат загружен)",
        "Completed" => "Завершен (работа принята)",
        _ => status
    };

    public class CreateOrderDto { public Guid CustomerId { get; set; } public Guid EngineerId { get; set; } public int ServiceId { get; set; } public decimal TotalAmount { get; set; } }
}