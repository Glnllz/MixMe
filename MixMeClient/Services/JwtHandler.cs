using System.Net.Http.Headers;
using Microsoft.JSInterop;

namespace MixMeClient.Services;

public class JwtHandler : DelegatingHandler
{
    private readonly IJSRuntime _js;

    public JwtHandler(IJSRuntime js)
    {
        _js = js;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Достаем токен из LocalStorage
        var token = await _js.InvokeAsync<string>("localStorage.getItem", "authToken");

        // Если токен есть — добавляем его в заголовок Authorization
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}