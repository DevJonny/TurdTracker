using Microsoft.JSInterop;

namespace TurdTracker.Services;

public class GoogleAuthService : IGoogleAuthService
{
    private const string ClientId = "101133685796-p43f4uqejgt9lgce4lvibrnoam8oegb1.apps.googleusercontent.com";

    private readonly IJSRuntime _jsRuntime;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public GoogleAuthService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            await _jsRuntime.InvokeVoidAsync("googleAuth.initialize", ClientId);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
    }

    public async Task<string?> SignInAsync()
    {
        await EnsureInitializedAsync();
        return await _jsRuntime.InvokeAsync<string?>("googleAuth.signIn");
    }

    public async Task SignOutAsync()
    {
        await _jsRuntime.InvokeVoidAsync("googleAuth.signOut");
    }

    public async Task<bool> IsSignedInAsync()
    {
        await EnsureInitializedAsync();
        return await _jsRuntime.InvokeAsync<bool>("googleAuth.isSignedIn");
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        await EnsureInitializedAsync();
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
        return await _jsRuntime.InvokeAsync<bool>("googleAuth.hasPreviousSession");
    }
}
