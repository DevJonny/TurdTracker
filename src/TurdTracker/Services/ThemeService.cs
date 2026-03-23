using Blazored.LocalStorage;

namespace TurdTracker.Services;

public class ThemeService : IThemeService
{
    private const string StorageKey = "theme-dark-mode";
    private readonly ILocalStorageService _localStorage;

    public ThemeService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task<bool> GetIsDarkModeAsync()
    {
        if (await _localStorage.ContainKeyAsync(StorageKey))
        {
            return await _localStorage.GetItemAsync<bool>(StorageKey);
        }
        // Default to dark mode
        return true;
    }

    public async Task SetIsDarkModeAsync(bool isDarkMode)
    {
        await _localStorage.SetItemAsync(StorageKey, isDarkMode);
    }
}
