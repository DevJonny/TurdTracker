using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using TurdTracker.Pages;
using TurdTracker.Services;
using TurdTracker.Tests.Fakes;
using Xunit;

namespace TurdTracker.Tests.Pages;

public class SettingsTests : IAsyncDisposable
{
    private readonly BunitContext _ctx;
    private readonly FakeGoogleAuthService _authService;
    private readonly FakeSyncService _syncService;
    private readonly FakeThemeService _themeService;

    public SettingsTests()
    {
        _ctx = new BunitContext();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();

        _authService = new FakeGoogleAuthService();
        _syncService = new FakeSyncService();
        _themeService = new FakeThemeService();

        _ctx.Services.AddSingleton<IGoogleAuthService>(_authService);
        _ctx.Services.AddSingleton<ISyncService>(_syncService);
        _ctx.Services.AddSingleton<IThemeService>(_themeService);
    }

    public async ValueTask DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    [Fact]
    public void SignInButton_CallsSignInAsync_OnSuccess_SetsSyncState()
    {
        _authService.IsSignedIn = false;
        _authService.SignInResult = "fake-token";
        var cut = _ctx.Render<Settings>();

        // Find and click the sign-in button
        var signInButton = cut.Find("button.mud-button-filled");
        signInButton.Click();

        // Wait for async operations
        cut.WaitForState(() => _authService.MethodCalls.Contains("SignInAsync"));

        _authService.MethodCalls.Should().Contain("SignInAsync");
        _syncService.MethodCalls.Should().Contain("SyncAsync");
        // After sign-in, should show signed-in UI (Sync Now button)
        cut.Markup.Should().Contain("Sync Now");
    }

    [Fact]
    public void SignOutButton_CallsSignOutAsync_ThenSyncAsync()
    {
        // Start signed in
        _authService.IsSignedIn = true;
        var cut = _ctx.Render<Settings>();
        cut.WaitForState(() => cut.Markup.Contains("Sync Now"));

        // Find sign-out button (outlined error button)
        var signOutButton = cut.FindAll("button.mud-button-outlined")
            .First(b => b.TextContent.Contains("Sign Out"));
        signOutButton.Click();

        cut.WaitForState(() => _authService.MethodCalls.Contains("SignOutAsync"));

        _authService.MethodCalls.Should().Contain("SignOutAsync");
        _syncService.MethodCalls.Should().Contain("SyncAsync");
        // After sign-out, should show sign-in button
        cut.Markup.Should().Contain("Sign in with Google");
    }

    [Fact]
    public void SyncNowButton_CallsSyncAsync()
    {
        _authService.IsSignedIn = true;
        var cut = _ctx.Render<Settings>();
        cut.WaitForState(() => cut.Markup.Contains("Sync Now"));

        var syncButton = cut.FindAll("button.mud-button-outlined")
            .First(b => b.TextContent.Contains("Sync Now"));
        syncButton.Click();

        _syncService.MethodCalls.Should().Contain("SyncAsync");
    }

    [Fact]
    public void ButtonsDisabled_WhenSyncStatusIsSyncing()
    {
        _authService.IsSignedIn = true;
        _syncService.SyncStatus = SyncStatus.Syncing;
        var cut = _ctx.Render<Settings>();
        cut.WaitForState(() => cut.Markup.Contains("Sync Now"));

        var syncButton = cut.FindAll("button.mud-button-outlined")
            .First(b => b.TextContent.Contains("Sync Now"));
        var signOutButton = cut.FindAll("button.mud-button-outlined")
            .First(b => b.TextContent.Contains("Sign Out"));

        syncButton.HasAttribute("disabled").Should().BeTrue();
        signOutButton.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void SyncStatusText_ReflectsCurrentStatus()
    {
        _authService.IsSignedIn = true;
        _syncService.SyncStatus = SyncStatus.Synced;
        var cut = _ctx.Render<Settings>();
        cut.WaitForState(() => cut.Markup.Contains("Connected and synced"));

        cut.Markup.Should().Contain("Connected and synced");

        // Change status and fire event
        _syncService.SyncStatus = SyncStatus.Syncing;
        _syncService.RaiseOnSyncStatusChanged();

        cut.WaitForState(() => cut.Markup.Contains("Syncing..."));
        cut.Markup.Should().Contain("Syncing...");

        // Error status
        _syncService.SyncStatus = SyncStatus.Error;
        _syncService.RaiseOnSyncStatusChanged();

        cut.WaitForState(() => cut.Markup.Contains("Sync error"));
        cut.Markup.Should().Contain("Sync error");
    }

    [Fact]
    public void LastSyncedUtc_DisplayedWhenAvailable()
    {
        _authService.IsSignedIn = true;
        _syncService.SyncStatus = SyncStatus.Synced;
        _syncService.LastSyncedUtc = new DateTime(2026, 3, 25, 14, 30, 0, DateTimeKind.Utc);
        var cut = _ctx.Render<Settings>();
        cut.WaitForState(() => cut.Markup.Contains("Last synced"));

        cut.Markup.Should().Contain("Last synced");
    }
}
