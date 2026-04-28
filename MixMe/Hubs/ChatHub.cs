using Microsoft.AspNetCore.SignalR;

namespace MixMe.Hubs;

public class ChatHub : Hub
{
    // Метод для подключения к комнате конкретного заказа
    public async Task JoinOrderGroup(string orderId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, orderId);
    }

    // Отправка сообщения участникам заказа
    public async Task SendMessage(string orderId, string user, string message)
    {
        await Clients.Group(orderId).SendAsync("ReceiveMessage", user, message);
    }
}