using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using TurdTracker.Services;
using TurdTracker.Tests.Fakes;
using Xunit;

namespace TurdTracker.Tests.Layout;

public class MainLayoutTests : IAsyncDisposable
{
    private readonly BunitContext _ctx;
    private readonly FakeSyncService _syncService;
    private readonly FakeThemeService _themeService;

    public MainLayoutTests()
    {
        _ctx = new BunitContext();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();

        _syncService = new FakeSyncService();
        _themeService = new FakeThemeService();

        _ctx.Services.AddSingleton<ISyncService>(_syncService);
        _ctx.Services.AddSingleton<IThemeService>(_themeService);
    }

    public async ValueTask DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    private IRenderedComponent<TurdTracker.Layout.MainLayout> RenderLayout()
    {
        var cut = _ctx.Render<TurdTracker.Layout.MainLayout>(parameters =>
            parameters.Add(p => p.Body, builder =>
            {
                builder.AddContent(0, "Test Body Content");
            }));
        return cut;
    }

    [Fact]
    public void SyncIcon_CloudDone_Success_WhenSynced()
    {
        _syncService.SyncStatus = SyncStatus.Synced;
        var cut = RenderLayout();

        // The sync icon button should have success color class
        var syncButtons = cut.FindAll("button.mud-icon-button");
        // Find the one with success color (mud-success-text)
        var syncButton = syncButtons.FirstOrDefault(b => b.ClassList.Contains("mud-success-text"));
        syncButton.Should().NotBeNull("sync icon should have Success color when Synced");
    }

    [Fact]
    public void SyncIcon_CloudSync_Inherit_WithSpinClass_WhenSyncing()
    {
        _syncService.SyncStatus = SyncStatus.Syncing;
        var cut = RenderLayout();

        // The sync icon button should have inherit color and sync-spinning class
        var syncButton = cut.FindAll("button.mud-icon-button")
            .FirstOrDefault(b => b.ClassList.Contains("sync-spinning"));
        syncButton.Should().NotBeNull("sync icon should have sync-spinning class when Syncing");
        syncButton!.ClassList.Should().Contain("mud-inherit-text",
            "sync icon should have Inherit color when Syncing");
    }

    [Fact]
    public void SyncIcon_CloudOff_Error_WhenError()
    {
        _syncService.SyncStatus = SyncStatus.Error;
        _syncService.LastError = "Something went wrong";
        var cut = RenderLayout();

        var syncButton = cut.FindAll("button.mud-icon-button")
            .FirstOrDefault(b => b.ClassList.Contains("mud-error-text"));
        syncButton.Should().NotBeNull("sync icon should have Error color when status is Error");
    }

    [Fact]
    public void SyncIcon_CloudOff_Default_WhenNotSignedIn()
    {
        _syncService.SyncStatus = SyncStatus.NotSignedIn;
        var cut = RenderLayout();

        // Color.Default renders without a specific color class in MudBlazor
        // The sync icon is inside MudTooltip — find icon buttons and look for the one
        // that is NOT inherit-text (theme toggle is inherit, sync with Default has no color class)
        var allIconButtons = cut.FindAll("button.mud-icon-button");
        // In NotSignedIn state, sync button should not have success/error/inherit color classes
        // (it uses Color.Default which MudBlazor renders without a color-text class)
        var syncButton = allIconButtons.FirstOrDefault(b =>
            !b.ClassList.Contains("mud-inherit-text") &&
            !b.ClassList.Contains("mud-success-text") &&
            !b.ClassList.Contains("mud-error-text") &&
            !b.ClassList.Contains("mud-primary-text") &&
            !b.ClassList.Contains("mud-secondary-text"));
        syncButton.Should().NotBeNull("sync icon should have Default color (no color class) when NotSignedIn");
    }

    [Fact]
    public void ClickingSyncIcon_DuringError_ShowsSnackbarWithLastError()
    {
        _syncService.SyncStatus = SyncStatus.Error;
        _syncService.LastError = "Upload failed: 500";
        var cut = RenderLayout();

        var syncButton = cut.FindAll("button.mud-icon-button")
            .First(b => b.ClassList.Contains("mud-error-text"));
        syncButton.Click();

        // MudBlazor snackbar renders messages in the snackbar provider
        cut.WaitForState(() => cut.Markup.Contains("Upload failed: 500"));
        cut.Markup.Should().Contain("Upload failed: 500");
    }

    [Fact]
    public void OnDataMerged_ShowsSyncCompleteSnackbar_WhenSynced()
    {
        _syncService.SyncStatus = SyncStatus.Synced;
        var cut = RenderLayout();

        _syncService.RaiseOnDataMerged();

        cut.WaitForState(() => cut.Markup.Contains("Sync complete"));
        cut.Markup.Should().Contain("Sync complete");
    }

    [Fact]
    public void OnDataMerged_ShowsSyncCompleteSnackbar_WhenIdle()
    {
        _syncService.SyncStatus = SyncStatus.Idle;
        var cut = RenderLayout();

        _syncService.RaiseOnDataMerged();

        cut.WaitForState(() => cut.Markup.Contains("Sync complete"));
        cut.Markup.Should().Contain("Sync complete");
    }

    [Fact]
    public void OnDataMerged_DoesNotShowSnackbar_WhenSyncing()
    {
        _syncService.SyncStatus = SyncStatus.Syncing;
        var cut = RenderLayout();

        _syncService.RaiseOnDataMerged();

        // Small delay to allow any async operations to complete
        Task.Delay(100).Wait();
        cut.Markup.Should().NotContain("Sync complete");
    }

    [Fact]
    public void ThemeToggle_SwitchesDarkLightMode()
    {
        _themeService.IsDarkMode = true;
        var cut = RenderLayout();

        // The theme toggle button is the icon button right after the MudTooltip (sync icon) in the appbar.
        // It's an inherit-colored icon button that is NOT inside a .mud-tooltip-root.
        // Find all appbar icon buttons, then pick the one after the tooltip root.
        var appbar = cut.Find(".mud-appbar");
        var appbarIconButtons = appbar.QuerySelectorAll("button.mud-icon-button.mud-inherit-text");
        // The theme toggle is the last inherit-colored button directly in the appbar
        // (the sync icon is inside .mud-tooltip-root and may or may not be inherit)
        // Filter out buttons inside .mud-tooltip-root
        var themeButton = appbarIconButtons
            .LastOrDefault(b => b.Closest(".mud-tooltip-root") == null);
        themeButton.Should().NotBeNull("should find theme toggle button");
        themeButton!.Click();

        _themeService.IsDarkMode.Should().BeFalse("clicking theme toggle in dark mode should switch to light");

        // Click again to go back to dark mode
        appbarIconButtons = cut.Find(".mud-appbar").QuerySelectorAll("button.mud-icon-button.mud-inherit-text");
        themeButton = appbarIconButtons.LastOrDefault(b => b.Closest(".mud-tooltip-root") == null);
        themeButton!.Click();

        _themeService.IsDarkMode.Should().BeTrue("clicking theme toggle in light mode should switch to dark");
    }

    [Fact]
    public void Tooltip_ShowsStatus_AndLastSyncedUtc()
    {
        _syncService.SyncStatus = SyncStatus.Synced;
        _syncService.LastSyncedUtc = new DateTime(2026, 3, 25, 14, 30, 0, DateTimeKind.Utc);
        var cut = RenderLayout();

        // MudTooltip renders tooltip content in popover on pointer enter
        var tooltipRoot = cut.Find(".mud-tooltip-root");
        tooltipRoot.Should().NotBeNull();
        tooltipRoot.PointerEnter();

        cut.WaitForState(() => cut.Markup.Contains("Synced"));
        cut.Markup.Should().Contain("Synced");
        cut.Markup.Should().Contain("Last synced");
    }

    [Fact]
    public void Tooltip_ShowsNotSyncedYet_WhenNoLastSyncedUtc()
    {
        _syncService.SyncStatus = SyncStatus.NotSignedIn;
        _syncService.LastSyncedUtc = null;
        var cut = RenderLayout();

        var tooltipRoot = cut.Find(".mud-tooltip-root");
        tooltipRoot.Should().NotBeNull();
        tooltipRoot.PointerEnter();

        cut.WaitForState(() => cut.Markup.Contains("Not signed in"));
        cut.Markup.Should().Contain("Not signed in");
        cut.Markup.Should().Contain("Not synced yet");
    }
}
