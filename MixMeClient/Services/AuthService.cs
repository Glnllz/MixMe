using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using MixMeShared;

namespace MixMeClient.Services;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    public UserProfile? User { get; private set; }
    public bool IsLoggedIn => User != null;

    public AuthService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    // Сохранение пользователя после логина
    public async Task Login(UserProfile user)
    {
        User = user;
        var json = JsonSerializer.Serialize(user);
        await _js.InvokeVoidAsync("localStorage.setItem", "currentUser", json);
    }

    // Загрузка пользователя при старте приложения
    public async Task InitializeAsync()
    {
        var json = await _js.InvokeAsync<string>("localStorage.getItem", "currentUser");
        if (!string.IsNullOrEmpty(json))
        {
            User = JsonSerializer.Deserialize<UserProfile>(json);
        }
    }

    // Выход
    public async Task Logout()
    {
        User = null;
        await _js.InvokeVoidAsync("localStorage.removeItem", "currentUser");
    }
}