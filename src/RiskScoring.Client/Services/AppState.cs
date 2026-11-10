using Microsoft.JSInterop;
using RiskScoring.Shared.Models;

namespace RiskScoring.Client.Services;

/// <summary>Holds the authenticated user and the PWA install state.</summary>
public class AppState
{
    private readonly IJSRuntime _js;
    private const string Key = "sentinel.user";

    public AuthUser? User { get; private set; }
    public bool IsAuthenticated => User is not null;
    public bool CanInstall { get; private set; }

    public event Action? Changed;

    public AppState(IJSRuntime js) => _js = js;

    public async Task EnsureLoadedAsync()
    {
        if (User is not null) return;
        try
        {
            var json = await _js.InvokeAsync<string?>("sentinel.store.get", Key);
            if (!string.IsNullOrWhiteSpace(json))
                User = System.Text.Json.JsonSerializer.Deserialize<AuthUser>(json);
        }
        catch { /* storage unavailable */ }
    }

    public async Task SignInAsync(AuthUser user)
    {
        User = user;
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(user);
            await _js.InvokeVoidAsync("sentinel.store.set", Key, json);
        }
        catch { }
        Changed?.Invoke();
    }

    public async Task SignOutAsync()
    {
        User = null;
        try { await _js.InvokeVoidAsync("sentinel.store.remove", Key); } catch { }
        Changed?.Invoke();
    }

    public void SetCanInstall(bool value)
    {
        CanInstall = value;
        Changed?.Invoke();
    }
}
