using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMeShared;
using MixMe.Services;
using Yandex.Checkout.V3;
using Microsoft.AspNetCore.SignalR;
using MixMe.Hubs;
using Microsoft.AspNetCore.Authorization;
using Notification = MixMeShared.Notification;

namespace MixMe.Controllers;

[Authorize]
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
            Status = "WaitingForConfirmation", // Начальный статус: инженер должен проверить
            CreatedAt = DateTime.UtcNow
        };

        _context.Orders.Add(newOrder);

        var service = await _context.Services.FindAsync(dto.ServiceId);
        string serviceName = service?.Name ?? "Индивидуальное предложение";

        // 1. Системное сообщение в чат
        string content = $"🤖 Проект '{serviceName}' создан. Инженер изучает исходники перед подтверждением.";
        _context.Messages.Add(new Message
        {
            OrderId = newOrder.Id,
            SenderId = Guid.Empty,
            Content = content,
            SentAt = DateTime.UtcNow
        });

        // 2. Уведомление в БД для инженера (чтобы он увидел колокольчик)
        _context.Notifications.Add(new MixMeShared.Notification
        {
            UserId = dto.EngineerId,
            Text = $"📩 Новый запрос на проект: {serviceName}. Требуется ваше подтверждение.",
            Link = $"/order/{newOrder.Id}",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        // 3. SignalR: оповещаем инженера мгновенно
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

        // Определяем, кто наш оппонент
        var partnerId = (currentUserId == order.CustomerId) ? order.EngineerId : order.CustomerId;
        var partner = await _context.Profiles.FindAsync(partnerId);

        return Ok(new
        {
            Id = order.Id,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            CustomerId = order.CustomerId,
            EngineerId = order.EngineerId,

            // Добавляем эти поля:
            PartnerId = partnerId,
            PartnerName = partner?.Username ?? "Партнер",
            PartnerAvatar = partner?.AvatarUrl,

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

        // Базовый текст сообщения
        string content = $"🤖 Статус заказа изменен на: {TranslateStatusForChat(newStatus)}";

        // --- ЛОГИКА УВЕДОМЛЕНИЙ ДЛЯ КАЖДОГО СТАТУСА ---

        if (newStatus == "WaitingForPayment") // Инженер подтвердил
        {
            content = "✅ Инженер подтвердил готовность! Заказчик, теперь вы можете оплатить проект.";
            _context.Notifications.Add(new MixMeShared.Notification
            {
                UserId = order.CustomerId,
                Text = "Инженер принял ваш заказ! Можно переходить к оплате.",
                Link = $"/order/{id}",
                CreatedAt = DateTime.UtcNow
            });
        }
        else if (newStatus == "InProgress") // Оплата прошла
        {
            content = "💰 Оплата подтверждена. Инженер приступил к выполнению работы.";
            // (Обычно уведомление здесь не нужно, так как оба видят системное сообщение в чате)
        }
        else if (newStatus == "UnderReview") // Инженер загрузил результат
        {
            content = "🚀 Работа выполнена! Заказчик, пожалуйста, проверьте результат и примите проект.";
            _context.Notifications.Add(new MixMeShared.Notification
            {
                UserId = order.CustomerId,
                Text = "Инженер загрузил финальный микс! Проверьте работу.",
                Link = $"/order/{id}",
                CreatedAt = DateTime.UtcNow
            });
        }
        else if (newStatus == "Completed") // Заказчик нажал "Принять"
        {
            order.CompletedAt = DateTime.UtcNow;
            content = "🎉 Проект успешно завершен! Спасибо за сотрудничество.";
            _context.Notifications.Add(new MixMeShared.Notification
            {
                UserId = order.EngineerId,
                Text = "Заказчик принял работу! Средства зачислены на ваш баланс.",
                Link = $"/order/{id}",
                CreatedAt = DateTime.UtcNow
            });
        }

        else if (newStatus == "Refused")
        {
            // Определяем, кто инициатор действия
            string initiator = (currentUserId == order.EngineerId) ? "Инженер отклонил выполнение проекта" : "Заказчик отменил запрос на проект";

            content = $"❌ {initiator}. Сделка закрыта.";
        }

        // Сохраняем сообщение в историю чата
        _context.Messages.Add(new Message { OrderId = id, SenderId = Guid.Empty, Content = content, SentAt = DateTime.UtcNow });

        await _context.SaveChangesAsync();

        // --- SIGNALR УВЕДОМЛЕНИЯ (Всем участникам) ---
        await _hubContext.Clients.Group(id.ToString()).SendAsync("ReceiveMessage", id.ToString(), "🤖", content);
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

            // --- НОВОЕ: Уведомление в БД для инженера об оплате ---
            _context.Notifications.Add(new Notification
            {
                UserId = order.EngineerId,
                Text = "Заказ оплачен! Можете приступать к выполнению проекта.",
                Link = $"/order/{order.Id}",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            // УВЕДОМЛЕНИЕ SignalR
            await _hubContext.Clients.Group(id.ToString()).SendAsync("ReceiveMessage", id.ToString(), "🤖", content);
            await _hubContext.Clients.Group(order.CustomerId.ToString()).SendAsync("ReceiveMessage", id.ToString(), "🤖", content);
            await _hubContext.Clients.Group(order.EngineerId.ToString()).SendAsync("ReceiveMessage", id.ToString(), "🤖", content);

            return Ok(new { paid = true });
        }
        return Ok(new { paid = false });
    }

    // MixMe/Controllers/OrdersController.cs

    [HttpPatch("{id}/decline")]
    public async Task<IActionResult> DeclineOrder(Guid id, [FromQuery] Guid currentUserId)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();

        // Отклонить можно только если заказ еще не оплачен (статус WaitingForPayment)
        if (order.Status != "WaitingForPayment")
            return BadRequest("Нельзя отклонить заказ, который уже оплачен или находится в работе.");

        // Меняем статус на Refused (Отклонено)
        order.Status = "Refused";

        // Кто именно отклонил?
        var user = await _context.Profiles.FindAsync(currentUserId);
        string initiatorType = (currentUserId == order.EngineerId) ? "Инженер" : "Заказчик";

        // Системное сообщение
        string content = $"❌ {initiatorType} {user.Username} отклонил запрос на проект. Сделка закрыта.";

        _context.Messages.Add(new Message
        {
            OrderId = id,
            SenderId = Guid.Empty,
            Content = content,
            SentAt = DateTime.UtcNow
        });

        // Уведомление второму участнику
        Guid recipientId = (currentUserId == order.CustomerId) ? order.EngineerId : order.CustomerId;
        _context.Notifications.Add(new Notification
        {
            UserId = recipientId,
            Text = $"Проект был отклонен участником {user.Username}.",
            Link = $"/order/{id}",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        // SignalR оповещение
        await _hubContext.Clients.Group(id.ToString()).SendAsync("ReceiveMessage", id.ToString(), "🤖", content);

        return Ok();
    }

    [HttpPatch("{id}/accept")]
    public async Task<IActionResult> AcceptOrder(Guid id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();

        if (order.Status != "WaitingForConfirmation")
            return BadRequest("Заказ нельзя принять в текущем статусе.");

        order.Status = "WaitingForPayment"; // Теперь можно платить

        string content = "✅ Инженер подтвердил готовность! Заказчик, теперь вы можете оплатить проект для начала работы.";
        _context.Messages.Add(new Message
        {
            OrderId = id,
            SenderId = Guid.Empty,
            Content = content,
            SentAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        await _hubContext.Clients.Group(id.ToString()).SendAsync("ReceiveMessage", id.ToString(), "🤖", content);

        return Ok();
    }

    private string TranslateStatusForChat(string status) => status switch
    {
        "WaitingForConfirmation" => "Ожидает подтверждения инженером",
        "WaitingForPayment" => "Ожидает оплаты заказчиком",
        "Refused" => "Отклонено инженером",
        "InProgress" => "В работе",
        "UnderReview" => "На проверке (результат загружен)",
        "Completed" => "Завершен (работа принята)",
        "Cancelled" => "Отменен",
        _ => status
    };

    public class CreateOrderDto { public Guid CustomerId { get; set; } public Guid EngineerId { get; set; } public int ServiceId { get; set; } public decimal TotalAmount { get; set; } }
}