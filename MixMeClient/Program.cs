using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MixMeClient;
using MixMeClient.Services;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Указываем корневые компоненты
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// --- НАСТРОЙКА JWT И HTTP CLIENT ---

// 1. Регистрируем перехватчик (Handler), который будет проверять наличие токена
builder.Services.AddTransient<JwtHandler>();

// 2. Создаем именованный клиент "MixMeAPI" и привязываем к нему наш перехватчик
builder.Services.AddHttpClient("MixMeAPI", client =>
{
    client.BaseAddress = new Uri("https://localhost:7237/");
})
.AddHttpMessageHandler<JwtHandler>();

// 3. Делаем этот настроенный клиент клиентом "по умолчанию".
// Теперь, когда ты пишешь @inject HttpClient Http в Razor-страницах, 
// Blazor будет использовать именно версию с поддержкой JWT.
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("MixMeAPI"));


// --- РЕГИСТРАЦИЯ ОСТАЛЬНЫХ СЕРВИСОВ ---
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<AuthService>();

await builder.Build().RunAsync();