using Microsoft.JSInterop;

namespace TurdTracker.Services;

public class GoogleAuthService : IGoogleAuthService
{
    private const string ClientId = "101133685796-p43f4uqejgt9lgce4lvibrnoam8oegb1.apps.googleusercontent.com";

    private readonly IJSRuntime _jsRuntime;
    private bool _initialized;

    public GoogleAuthService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
            _initialized = true;
        }
    }

    public async Task InitializeAsync()
    {
        await _jsRuntime.InvokeVoidAsync("googleAuth.initialize", ClientId);
        _initialized = true;
    }

    public async Task<string?> SignInAsync()
    {
        return await _jsRuntime.InvokeAsync<string?>("googleAuth.signIn");
    }

    public async Task SignOutAsync()
    {
        await _jsRuntime.InvokeVoidAsync("googleAuth.signOut");
    }

    public async Task<bool> IsSignedInAsync()
    {
        return await _jsRuntime.InvokeAsync<bool>("googleAuth.isSignedIn");
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        return await _jsRuntime.InvokeAsync<string?>("googleAuth.getAccessToken");
    }

    public async Task<bool> TrySilentSignInAsync()
    {
        await EnsureInitializedAsync();
        var token = await _jsRuntime.InvokeAsync<string?>("googleAuth.trySilentSignIn");
        return token is not null;
    }

    public async Task<bool> HasPreviousSessionAsync()
    {
        await EnsureInitializedAsync();
        return await _jsRuntime.InvokeAsync<bool>("googleAuth.hasPreviousSession");
    }
}
