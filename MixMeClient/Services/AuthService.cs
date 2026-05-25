using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using MixMeShared;

namespace MixMeClient.Services;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    public event Action? OnLoginStateChanged;
    public UserProfile? User { get; private set; }
    public bool IsLoggedIn => User != null;

    public AuthService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    // Сохранение пользователя после логина
    public async Task Login(string token, UserProfile user)
    {
        User = user;
        await _js.InvokeVoidAsync("localStorage.setItem", "authToken", token);
        var json = JsonSerializer.Serialize(user);
        await _js.InvokeVoidAsync("localStorage.setItem", "currentUser", json);

        // ВЫЗЫВАЕМ СОБЫТИЕ
        OnLoginStateChanged?.Invoke();
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
    public async Task UpdateUser(UserProfile user)
    {
        User = user;
        var json = JsonSerializer.Serialize(user);
        await _js.InvokeVoidAsync("localStorage.setItem", "currentUser", json);
    }

    // Выход
    public async Task Logout()
    {
        User = null;
        await _js.InvokeVoidAsync("localStorage.removeItem", "authToken");
        await _js.InvokeVoidAsync("localStorage.removeItem", "currentUser");
    }
}