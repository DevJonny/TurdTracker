namespace TurdTracker.Services;

public interface IGoogleAuthService
{
    Task InitializeAsync();
    Task<string?> SignInAsync();
    Task SignOutAsync();
    Task<bool> IsSignedInAsync();
    Task<string?> GetAccessTokenAsync();
    Task<bool> TrySilentSignInAsync();
    Task<bool> HasPreviousSessionAsync();
}
