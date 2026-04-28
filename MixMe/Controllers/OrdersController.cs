using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMeShared;
using MixMe.Services;
using Yandex.Checkout.V3;

namespace MixMe.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly PaymentService _paymentService;

    public OrdersController(ApplicationDbContext context, PaymentService paymentService)
    {
        _context = context;
        _paymentService = paymentService;
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
            Status = "WaitingForPayment", // Начальный статус
            CreatedAt = DateTime.UtcNow
        };

        _context.Orders.Add(newOrder);
        await _context.SaveChangesAsync();

        // Создаем первое системное сообщение в чате
        var service = await _context.Services.FindAsync(dto.ServiceId);
        _context.Messages.Add(new Message
        {
            OrderId = newOrder.Id,
            SenderId = Guid.Empty, // Системное сообщение
            Content = $"Заказ на услугу '{service?.Name}' создан. Ожидается оплата.",
            SentAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        return Ok(newOrder.Id); // Возвращаем ID нового заказа
    }

    // Вспомогательный класс для передачи данных
    public class CreateOrderDto
    {
        public Guid CustomerId { get; set; }
        public Guid EngineerId { get; set; }
        public int ServiceId { get; set; }
        public decimal TotalAmount { get; set; }
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<object>>> GetUserOrders(Guid userId)
    {
        var orders = await _context.Orders
            .Where(o => o.CustomerId == userId || o.EngineerId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new {
                Id = o.Id, // <--- Оставляем просто Id для всех
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                PartnerName = _context.Profiles
                    .Where(p => p.Id == (o.CustomerId == userId ? o.EngineerId : o.CustomerId))
                    .Select(p => p.Username).FirstOrDefault(),
                PartnerAvatar = _context.Profiles
                    .Where(p => p.Id == (o.CustomerId == userId ? o.EngineerId : o.CustomerId))
                    .Select(p => p.AvatarUrl).FirstOrDefault(),
                // Добавляем текст последнего сообщения для страницы Чатов
                LastMessage = _context.Messages
                    .Where(m => m.OrderId == o.Id)
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => m.Content)
                    .FirstOrDefault() ?? "Чат создан",
                ServiceName = _context.Services.Where(s => s.Id == o.ServiceId).Select(s => s.Name).FirstOrDefault()
            })
            .ToListAsync();

        return Ok(orders);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetOrderDetails(Guid id)
    {
        var order = await _context.Orders
            .Select(o => new {
                o.Id,
                o.Status,
                o.TotalAmount,
                o.CustomerId,
                o.EngineerId,
                CustomerName = _context.Profiles.Where(p => p.Id == o.CustomerId).Select(p => p.Username).FirstOrDefault(),
                EngineerName = _context.Profiles.Where(p => p.Id == o.EngineerId).Select(p => p.Username).FirstOrDefault(),
                ServiceName = _context.Services.Where(s => s.Id == o.ServiceId).Select(s => s.Name).FirstOrDefault(),
                // История сообщений из БД (опционально)
                Messages = _context.Messages.Where(m => m.OrderId == id).OrderBy(m => m.SentAt).ToList()
            })
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();
        return Ok(order);
    }

    // Смена статуса заказа (например, "Принять работу")
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromQuery] string newStatus)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();
        order.Status = newStatus;
        await _context.SaveChangesAsync();
        return Ok();
    }
}