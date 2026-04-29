using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using System.Text.Json; // Для работы с JSON

[ApiController]
[Route("api/payment-callback")]
public class PaymentWebhookController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PaymentWebhookController(ApplicationDbContext context) => _context = context;

    [HttpPost]
    public async Task<IActionResult> Handle()
    {
        try
        {
            // 1. Читаем тело запроса
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync();

            // 2. Парсим JSON вручную (самый надежный способ)
            using var jsonDoc = JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;

            // Проверяем, что это уведомление о платеже
            if (root.TryGetProperty("object", out var paymentObject))
            {
                var paymentId = paymentObject.GetProperty("id").GetString();
                var status = paymentObject.GetProperty("status").GetString();

                // Если платеж успешно завершен (succeeded)
                if (status == "succeeded" && !string.IsNullOrEmpty(paymentId))
                {
                    // 3. Ищем заказ в нашей базе по PaymentId
                    var order = await _context.Orders.FirstOrDefaultAsync(o => o.PaymentId == paymentId);

                    if (order != null && order.Status == "WaitingForPayment")
                    {
                        // 4. Обновляем статус заказа
                        order.Status = "InProgress";
                        order.PaymentStatus = "succeeded";

                        // Добавляем системное сообщение в чат для истории
                        _context.Messages.Add(new MixMeShared.Message
                        {
                            OrderId = order.Id,
                            SenderId = Guid.Empty, // Системный отправитель
                            Content = "💰 Оплата подтверждена ЮKassa. Средства зарезервированы. Исполнитель может начинать работу.",
                            SentAt = DateTime.UtcNow
                        });

                        await _context.SaveChangesAsync();
                        Console.WriteLine($"[Payment] Заказ {order.Id} успешно оплачен.");
                    }
                }
            }

            return Ok(); // Всегда возвращаем 200 OK для ЮKassa, чтобы она не слала повторы
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Payment Error] {ex.Message}");
            return Ok(); // Возвращаем OK даже при ошибке, чтобы остановить поток уведомлений, если JSON битый
        }
    }
}