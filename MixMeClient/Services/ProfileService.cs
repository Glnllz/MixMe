using System.Net.Http.Json;
using MixMeShared;

namespace MixMeClient.Services;

public class ProfileService
{
    private readonly HttpClient _http;
    public UserProfile? CurrentUser { get; private set; }

    public ProfileService(HttpClient http)
    {
        _http = http;
    }

    public void SetUser(UserProfile user) => CurrentUser = user;

    public async Task<UserProfile?> GetProfileAsync(Guid id)
    {
        return await _http.GetFromJsonAsync<UserProfile>($"api/profiles/{id}");
    }
}