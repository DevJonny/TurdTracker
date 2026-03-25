using TurdTracker.Services;

namespace TurdTracker.Tests.Fakes;

public class FakeGoogleAuthService : IGoogleAuthService
{
    public bool IsSignedIn { get; set; }
    public string? AccessToken { get; set; }
    public bool SilentSignInResult { get; set; }
    public bool HasPreviousSession { get; set; }
    public string? SignInResult { get; set; }
    public List<string> MethodCalls { get; } = [];

    public Task InitializeAsync()
    {
        MethodCalls.Add(nameof(InitializeAsync));
        return Task.CompletedTask;
    }

    public Task<string?> SignInAsync()
    {
        MethodCalls.Add(nameof(SignInAsync));
        return Task.FromResult(SignInResult);
    }

    public Task SignOutAsync()
    {
        MethodCalls.Add(nameof(SignOutAsync));
        IsSignedIn = false;
        return Task.CompletedTask;
    }

    public Task<bool> IsSignedInAsync()
    {
        MethodCalls.Add(nameof(IsSignedInAsync));
        return Task.FromResult(IsSignedIn);
    }

    public Task<string?> GetAccessTokenAsync()
    {
        MethodCalls.Add(nameof(GetAccessTokenAsync));
        return Task.FromResult(AccessToken);
    }

    public Task<bool> TrySilentSignInAsync()
    {
        MethodCalls.Add(nameof(TrySilentSignInAsync));
        return Task.FromResult(SilentSignInResult);
    }

    public Task<bool> HasPreviousSessionAsync()
    {
        MethodCalls.Add(nameof(HasPreviousSessionAsync));
        return Task.FromResult(HasPreviousSession);
    }
}
