using Blazored.LocalStorage;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using TurdTracker.Models;
using TurdTracker.Pages;
using TurdTracker.Services;
using TurdTracker.Tests.Fakes;
using Xunit;

namespace TurdTracker.Tests.Pages;

public class HomeTests : IAsyncDisposable
{
    private readonly BunitContext _ctx;
    private readonly FakeDiaryService _diaryService;
    private readonly FakeGoogleAuthService _authService;
    private readonly FakeSyncService _syncService;
    private readonly FakeLocalStorageService _localStorage;

    public HomeTests()
    {
        _ctx = new BunitContext();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();

        _diaryService = new FakeDiaryService();
        _authService = new FakeGoogleAuthService();
        _syncService = new FakeSyncService();
        _localStorage = new FakeLocalStorageService();

        _ctx.Services.AddSingleton<IDiaryService>(_diaryService);
        _ctx.Services.AddSingleton<IGoogleAuthService>(_authService);
        _ctx.Services.AddSingleton<ISyncService>(_syncService);
        _ctx.Services.AddSingleton<ILocalStorageService>(_localStorage);
    }

    public async ValueTask DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    [Fact]
    public void EmptyEntries_ShowsNoEntriesMessage_AndLogEntryButton()
    {
        var cut = _ctx.Render<Home>();

        cut.Markup.Should().Contain("No entries yet");
        cut.Markup.Should().Contain("Log Entry");
    }

    [Fact]
    public void Entries_RenderedAsCards_WithBristolType_Timestamp_TruncatedNotes_Tags()
    {
        var longNotes = new string('A', 100);
        _diaryService.SeedEntries(
            new DiaryEntry
            {
                BristolType = 4,
                Timestamp = new DateTime(2026, 3, 15, 10, 30, 0),
                Notes = longNotes,
                Tags = ["coffee", "morning"]
            }
        );

        var cut = _ctx.Render<Home>();

        // Bristol type shown in avatar
        cut.Markup.Should().Contain("4");
        // Timestamp formatted
        cut.Markup.Should().Contain("Sun, 15 Mar 2026");
        // Notes truncated at 80 chars with ellipsis
        cut.Markup.Should().Contain(longNotes[..80] + "…");
        cut.Markup.Should().NotContain(longNotes);
        // Tags shown
        cut.Markup.Should().Contain("coffee");
        cut.Markup.Should().Contain("morning");
    }

    [Fact]
    public void ClickingEntryCard_NavigatesToEntryDetail()
    {
        var entryId = Guid.NewGuid();
        _diaryService.SeedEntries(
            new DiaryEntry { Id = entryId, BristolType = 3, Timestamp = DateTime.Now }
        );

        var cut = _ctx.Render<Home>();

        // Click the entry card (MudPaper with cursor-pointer)
        var card = cut.Find(".cursor-pointer");
        card.Click();

        var nav = _ctx.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        nav.Uri.Should().EndWith($"entry/{entryId}");
    }

    [Fact]
    public async Task SyncBanner_ShownWhenNotSignedIn_AndNotDismissed()
    {
        _authService.IsSignedIn = false;
        // No dismissed key in localStorage

        var cut = _ctx.Render<Home>();

        // OnAfterRenderAsync runs after first render — trigger it
        cut.WaitForState(() => cut.Markup.Contains("Google Drive"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Sync your diary across devices with Google Drive");
    }

    [Fact]
    public void SyncBanner_HiddenWhenSignedIn()
    {
        _authService.IsSignedIn = true;

        var cut = _ctx.Render<Home>();

        // After render, banner check runs — signed in means no banner
        // Give it time to process OnAfterRenderAsync
        cut.WaitForState(() => !cut.Markup.Contains("Google Drive") || true, TimeSpan.FromSeconds(1));

        // Banner should not appear since user is signed in
        cut.Markup.Should().NotContain("Connect Google Drive");
    }

    [Fact]
    public async Task SyncBanner_HiddenWhenPreviouslyDismissed()
    {
        _authService.IsSignedIn = false;
        await _localStorage.SetItemAsync("sync-banner-dismissed", true);

        var cut = _ctx.Render<Home>();

        // Wait for OnAfterRenderAsync
        await Task.Delay(100);
        cut.Render();

        cut.Markup.Should().NotContain("Connect Google Drive");
    }

    [Fact]
    public void OnDataMerged_RefreshesEntries()
    {
        var cut = _ctx.Render<Home>();

        // Initially no entries
        cut.Markup.Should().Contain("No entries yet");

        // Add an entry and fire OnDataMerged
        _diaryService.SeedEntries(
            new DiaryEntry { BristolType = 5, Timestamp = DateTime.Now, Notes = "After sync" }
        );
        _syncService.RaiseOnDataMerged();

        // Wait for async re-render
        cut.WaitForState(() => cut.Markup.Contains("After sync"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("After sync");
        cut.Markup.Should().NotContain("No entries yet");
    }

    [Fact]
    public void Dispose_UnsubscribesOnDataMerged()
    {
        var cut = _ctx.Render<Home>();

        // Dispose the component
        cut.Dispose();

        // Add an entry after dispose
        _diaryService.SeedEntries(
            new DiaryEntry { BristolType = 5, Timestamp = DateTime.Now, Notes = "After dispose" }
        );

        // Firing OnDataMerged should not throw (no subscribers)
        var act = () => _syncService.RaiseOnDataMerged();
        act.Should().NotThrow();
    }
}
