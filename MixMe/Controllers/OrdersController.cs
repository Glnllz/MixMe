using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMeShared;
using MixMe.Services;
using Yandex.Checkout.V3;
using Microsoft.AspNetCore.SignalR;
using MixMe.Hubs;

namespace MixMe.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext;

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

        var content = $"🤖 Проект '{serviceName}' создан. Ожидается оплата заказчиком.";
        var systemMsg = new Message
        {
            OrderId = newOrder.Id,
            SenderId = Guid.Empty,
            Content = content,
            SentAt = DateTime.UtcNow
        };

        _context.Messages.Add(systemMsg);
        await _context.SaveChangesAsync();

        // УВЕДОМЛЕНИЕ: Отправляем инженеру персонально, что у него новый заказ
        await _hubContext.Clients.Group(dto.EngineerId.ToString()).SendAsync("ReceiveMessage", newOrder.Id.ToString(), "🤖", content);

        return Ok(newOrder.Id);
    }

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
                PartnerName = _context.Profiles
                    .Where(p => p.Id == (o.CustomerId == userId ? o.EngineerId : o.CustomerId))
                    .Select(p => p.Username)
                    .FirstOrDefault(),
                PartnerAvatar = _context.Profiles
                    .Where(p => p.Id == (o.CustomerId == userId ? o.EngineerId : o.CustomerId))
                    .Select(p => p.AvatarUrl)
                    .FirstOrDefault(),
                LastMessage = _context.Messages
                    .Where(m => m.OrderId == o.Id)
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => m.Content)
                    .FirstOrDefault() ?? "Проект создан",
                LastMessageTime = _context.Messages
                    .Where(m => m.OrderId == o.Id)
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => (DateTime?)m.SentAt)
                    .FirstOrDefault() ?? o.CreatedAt,
                ServiceName = _context.Services
                    .Where(s => s.Id == o.ServiceId)
                    .Select(s => s.Name)
                    .FirstOrDefault() ?? "Индивидуальный заказ"
            }).ToListAsync());
    }


    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetOrderDetails(Guid id, [FromQuery] Guid currentUserId)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();

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

        // УВЕДОМЛЕНИЕ: 1. В группу заказа (для тех, кто внутри чата)
        await _hubContext.Clients.Group(id.ToString()).SendAsync("ReceiveMessage", id.ToString(), "🤖", content);

        // УВЕДОМЛЕНИЕ: 2. Персонально обоим участникам (чтобы сработала точка в MainLayout на других страницах)
        await _hubContext.Clients.Group(order.CustomerId.ToString()).SendAsync("ReceiveMessage", id.ToString(), "🤖", content);
        await _hubContext.Clients.Group(order.EngineerId.ToString()).SendAsync("ReceiveMessage", id.ToString(), "🤖", content);

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

            // УВЕДОМЛЕНИЕ: В группу заказа
            await _hubContext.Clients.Group(id.ToString()).SendAsync("ReceiveMessage", id.ToString(), "🤖", content);

            // УВЕДОМЛЕНИЕ: Персонально участникам
            await _hubContext.Clients.Group(order.CustomerId.ToString()).SendAsync("ReceiveMessage", id.ToString(), "🤖", content);
            await _hubContext.Clients.Group(order.EngineerId.ToString()).SendAsync("ReceiveMessage", id.ToString(), "🤖", content);

            return Ok(new { paid = true });
        }
        return Ok(new { paid = false });
    }

    private string TranslateStatusForChat(string status) => status switch
    {
        "InProgress" => "В работе",
        "UnderReview" => "На проверке (результат загружен)",
        "Completed" => "Завершен (работа принята)",
        "Cancelled" => "Отменен",
        _ => status
    };

    public class CreateOrderDto { public Guid CustomerId { get; set; } public Guid EngineerId { get; set; } public int ServiceId { get; set; } public decimal TotalAmount { get; set; } }
}