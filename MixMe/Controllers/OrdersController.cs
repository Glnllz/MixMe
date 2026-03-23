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

    [HttpPost]
    public async Task<ActionResult<string>> CreateOrder([FromBody] Order order)
    {
        try
        {
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            string returnUrl = "https://localhost:7246/order-success/" + order.Id;

            var payment = await _paymentService.CreateOrderPayment(order, returnUrl);

            order.PaymentId = payment.Id;
            order.Status = "WaitingForPayment";
            await _context.SaveChangesAsync();

            // ИСПРАВЛЕНИЕ ЗДЕСЬ:
            // В версии 4.x чаще всего ссылка лежит напрямую в payment.Confirmation.ConfirmationUrl
            if (payment.Confirmation?.ConfirmationUrl != null)
            {
                return Ok(payment.Confirmation.ConfirmationUrl);
            }

            return BadRequest("Не удалось получить ссылку на оплату. Проверьте параметры подтверждения.");
        }
        catch (Exception ex)
        {
            return BadRequest($"Ошибка при создании платежа: {ex.Message}");
        }
    }
}