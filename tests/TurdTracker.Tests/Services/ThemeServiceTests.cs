using FluentAssertions;
using TurdTracker.Services;
using TurdTracker.Tests.Fakes;
using Xunit;

namespace TurdTracker.Tests.Services;

public class ThemeServiceTests
{
    private readonly FakeLocalStorageService _localStorage;
    private readonly ThemeService _sut;

    public ThemeServiceTests()
    {
        _localStorage = new FakeLocalStorageService();
        _sut = new ThemeService(_localStorage);
    }

    [Fact]
    public async Task GetIsDarkModeAsync_ReturnsTrue_WhenLocalStorageHasNoValue()
    {
        var result = await _sut.GetIsDarkModeAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetIsDarkModeAsync_ReturnsStoredValue_WhenPresent()
    {
        await _localStorage.SetItemAsync("theme-dark-mode", false);

        var result = await _sut.GetIsDarkModeAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetIsDarkModeAsync_WritesToLocalStorage()
    {
        await _sut.SetIsDarkModeAsync(false);

        var stored = await _localStorage.GetItemAsync<bool>("theme-dark-mode");
        stored.Should().BeFalse();
    }
}
