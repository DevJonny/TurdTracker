namespace TurdTracker.Services;

public interface IThemeService
{
    Task<bool> GetIsDarkModeAsync();
    Task SetIsDarkModeAsync(bool isDarkMode);
}
