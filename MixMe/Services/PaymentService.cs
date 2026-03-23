using Yandex.Checkout.V3;
using MixMeShared;
using System.Net.Http; // Не забудьте

namespace MixMe.Services;

public class PaymentService
{
    private readonly AsyncClient _client;

    // Принимаем IHttpClientFactory из системы (DI)
    public PaymentService(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        var shopId = config["Yookassa:ShopId"];
        var secretKey = config["Yookassa:SecretKey"];

        // Получаем правильно настроенный HttpClient из фабрики
        var httpClient = httpClientFactory.CreateClient();

        // Создаем авторизацию
        var yooKassaClient = new Client(shopId, secretKey);

        // Передаем всё в AsyncClient (3 параметра, как требовала ошибка)
        _client = new AsyncClient(httpClient, false, yooKassaClient);
    }

    public async Task<Payment> CreateOrderPayment(Order order, string returnUrl)
    {
        var newPayment = new NewPayment
        {
            Amount = new Amount { Value = order.TotalAmount, Currency = "RUB" },
            Confirmation = new Confirmation // Используем базовый класс
            {
                Type = ConfirmationType.Redirect,
                ReturnUrl = returnUrl
            },
            Description = $"Оплата заказа MixMe №{order.Id}",
            Capture = false // Обычно для магазинов лучше сразу Capture = true
        };

        return await _client.CreatePaymentAsync(newPayment);
    }
}