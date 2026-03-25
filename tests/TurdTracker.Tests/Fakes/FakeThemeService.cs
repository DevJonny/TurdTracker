using TurdTracker.Services;

namespace TurdTracker.Tests.Fakes;

public class FakeThemeService : IThemeService
{
    public bool IsDarkMode { get; set; } = true;

    public Task<bool> GetIsDarkModeAsync()
    {
        return Task.FromResult(IsDarkMode);
    }

    public Task SetIsDarkModeAsync(bool isDarkMode)
    {
        IsDarkMode = isDarkMode;
        return Task.CompletedTask;
    }
}
