using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using TurdTracker.Models;
using TurdTracker.Pages;
using TurdTracker.Services;
using TurdTracker.Tests.Fakes;
using Xunit;

namespace TurdTracker.Tests.Pages;

public class EntryDetailTests : IAsyncDisposable
{
    private readonly BunitContext _ctx;
    private readonly FakeDiaryService _diaryService;
    private readonly FakeDialogService _dialogService;

    public EntryDetailTests()
    {
        _ctx = new BunitContext();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();

        _diaryService = new FakeDiaryService();
        _dialogService = new FakeDialogService();

        _ctx.Services.AddSingleton<IDiaryService>(_diaryService);
        _ctx.Services.AddSingleton<IDialogService>(_dialogService);
    }

    public async ValueTask DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    private IRenderedComponent<EntryDetail> RenderEntryDetail(Guid id)
    {
        _ctx.Render<MudPopoverProvider>();
        return _ctx.Render<EntryDetail>(parameters =>
            parameters.Add(p => p.Id, id));
    }

    [Fact]
    public void LoadsAndDisplaysEntry_ByIdWithAllFields()
    {
        var entryId = Guid.NewGuid();
        var entry = new DiaryEntry
        {
            Id = entryId,
            Timestamp = new DateTime(2026, 3, 15, 14, 30, 0),
            BristolType = 4,
            Notes = "Test notes here",
            Tags = ["coffee", "morning"]
        };
        _diaryService.SeedEntries(entry);

        var cut = RenderEntryDetail(entryId);

        // Bristol type displayed
        cut.Markup.Should().Contain("Type 4");

        // Timestamp displayed
        cut.Markup.Should().Contain("Sunday, 15 March 2026");

        // Notes displayed
        cut.Markup.Should().Contain("Test notes here");

        // Tags displayed
        cut.Markup.Should().Contain("coffee");
        cut.Markup.Should().Contain("morning");
    }

    [Fact]
    public void EntryNotFound_ShowsErrorMessage()
    {
        var nonExistentId = Guid.NewGuid();

        var cut = RenderEntryDetail(nonExistentId);

        cut.Markup.Should().Contain("Entry not found");
    }

    [Fact]
    public void EditButton_TogglesEditFormWithPrePopulatedFields()
    {
        var entryId = Guid.NewGuid();
        var entry = new DiaryEntry
        {
            Id = entryId,
            Timestamp = new DateTime(2026, 3, 15, 14, 30, 0),
            BristolType = 4,
            Notes = "Original notes",
            Tags = ["tag1"]
        };
        _diaryService.SeedEntries(entry);

        var cut = RenderEntryDetail(entryId);

        // Verify we're in view mode
        cut.Markup.Should().Contain("Entry Detail");

        // Click Edit button (MudIconButton with edit icon)
        var editButton = cut.Find("button.mud-icon-button.mud-primary-text");
        editButton.Click();

        // Should now show edit form
        cut.Markup.Should().Contain("Edit Entry");

        // Notes field should be pre-populated
        cut.Markup.Should().Contain("Original notes");
    }

    [Fact]
    public void SaveInEditMode_CallsUpdateAsync_AndExitsEditMode()
    {
        var entryId = Guid.NewGuid();
        var entry = new DiaryEntry
        {
            Id = entryId,
            Timestamp = new DateTime(2026, 3, 15, 14, 30, 0),
            BristolType = 4,
            Notes = "Original notes",
            Tags = ["tag1"]
        };
        _diaryService.SeedEntries(entry);

        var cut = RenderEntryDetail(entryId);

        // Enter edit mode
        var editButton = cut.Find("button.mud-icon-button.mud-primary-text");
        editButton.Click();

        // Click Save
        var saveButton = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Save"));
        saveButton.Click();

        // Wait for async save
        cut.WaitForState(() => _diaryService.MethodCalls.Contains("UpdateAsync"));

        // UpdateAsync was called
        _diaryService.MethodCalls.Should().Contain("UpdateAsync");

        // Should exit edit mode back to view
        cut.Markup.Should().Contain("Entry Detail");
        cut.Markup.Should().NotContain("Edit Entry");
    }

    [Fact]
    public void CancelInEditMode_ExitsWithoutSaving()
    {
        var entryId = Guid.NewGuid();
        var entry = new DiaryEntry
        {
            Id = entryId,
            Timestamp = new DateTime(2026, 3, 15, 14, 30, 0),
            BristolType = 4,
            Notes = "Original notes",
            Tags = []
        };
        _diaryService.SeedEntries(entry);

        var cut = RenderEntryDetail(entryId);

        // Enter edit mode
        var editButton = cut.Find("button.mud-icon-button.mud-primary-text");
        editButton.Click();

        cut.Markup.Should().Contain("Edit Entry");

        // Click Cancel
        var cancelButton = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Cancel"));
        cancelButton.Click();

        // Should return to view mode without calling UpdateAsync
        cut.Markup.Should().Contain("Entry Detail");
        _diaryService.MethodCalls.Should().NotContain("UpdateAsync");
    }

    [Fact]
    public void Delete_ShowsConfirmation_ConfirmCallsDeleteAsync_AndNavigatesToHome()
    {
        var entryId = Guid.NewGuid();
        var entry = new DiaryEntry
        {
            Id = entryId,
            Timestamp = new DateTime(2026, 3, 15, 14, 30, 0),
            BristolType = 4,
            Notes = "To be deleted",
            Tags = []
        };
        _diaryService.SeedEntries(entry);
        _dialogService.MessageBoxResult = true; // Confirm delete

        var cut = RenderEntryDetail(entryId);
        var nav = _ctx.Services.GetRequiredService<NavigationManager>();

        // Click Delete button
        var deleteButton = cut.Find("button.mud-icon-button.mud-error-text");
        deleteButton.Click();

        // Wait for async operations
        cut.WaitForState(() => _diaryService.MethodCalls.Contains("DeleteAsync"));

        // ShowMessageBoxAsync was called (confirmation dialog)
        _dialogService.MethodCalls.Should().Contain("ShowMessageBoxAsync");

        // DeleteAsync was called
        _diaryService.MethodCalls.Should().Contain("DeleteAsync");

        // Navigated to home
        nav.Uri.Should().EndWith("/");
    }
}
