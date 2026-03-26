using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using TurdTracker.Models;
using TurdTracker.Pages;
using TurdTracker.Services;
using TurdTracker.Tests.Fakes;
using Xunit;

namespace TurdTracker.Tests.Pages;

public class ExportTests : IAsyncDisposable
{
    private readonly BunitContext _ctx;
    private readonly FakeDiaryService _diaryService;
    private readonly FakeSyncService _syncService;

    public ExportTests()
    {
        _ctx = new BunitContext();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();

        _diaryService = new FakeDiaryService();
        _syncService = new FakeSyncService();

        _ctx.Services.AddSingleton<IDiaryService>(_diaryService);
        _ctx.Services.AddSingleton<ISyncService>(_syncService);
    }

    public async ValueTask DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    private IRenderedComponent<Export> RenderExport()
    {
        _ctx.Render<MudPopoverProvider>();
        return _ctx.Render<Export>();
    }

    private static DiaryEntry CreateEntry(DateTime timestamp, int bristolType = 4, string? notes = null, List<string>? tags = null)
    {
        return new DiaryEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = timestamp,
            BristolType = bristolType,
            Notes = notes ?? "",
            Tags = tags ?? [],
            LastModified = DateTime.UtcNow
        };
    }

    [Fact]
    public void AllEntriesShown_WhenNoDateFilterApplied()
    {
        _diaryService.SeedEntries(
            CreateEntry(new DateTime(2026, 3, 1, 10, 0, 0), 3, "First"),
            CreateEntry(new DateTime(2026, 3, 15, 14, 0, 0), 4, "Second"),
            CreateEntry(new DateTime(2026, 3, 25, 9, 0, 0), 5, "Third")
        );

        var cut = RenderExport();

        // Print table should show all 3 entries
        var rows = cut.FindAll(".print-table tbody tr");
        rows.Count.Should().Be(3);

        // All entries subtitle should say "All entries"
        cut.Markup.Should().Contain("All entries");
        cut.Markup.Should().Contain("3 entries");
    }

    [Fact]
    public void StartDateFilter_ExcludesEntriesBefore()
    {
        _diaryService.SeedEntries(
            CreateEntry(new DateTime(2026, 3, 1, 10, 0, 0), 3, "Before"),
            CreateEntry(new DateTime(2026, 3, 10, 14, 0, 0), 4, "After start"),
            CreateEntry(new DateTime(2026, 3, 20, 9, 0, 0), 5, "Well after")
        );

        var cut = RenderExport();

        // All 3 entries initially
        cut.FindAll(".print-table tbody tr").Count.Should().Be(3);

        // Set start date via component instance using reflection to access private field
        // Since we can't easily interact with MudDatePicker in bUnit,
        // we set the field and call PrintExport which re-applies the filter
        var component = cut.Instance;
        typeof(Export).GetField("_startDate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(component, (DateTime?)new DateTime(2026, 3, 5));

        // Click Export button to trigger ApplyFilter + print
        var exportButton = cut.Find("button.mud-button-filled");
        exportButton.Click();

        // Should only show entries on or after March 5
        var rows = cut.FindAll(".print-table tbody tr");
        rows.Count.Should().Be(2);
        cut.Markup.Should().Contain("After start");
        cut.Markup.Should().Contain("Well after");
        cut.Markup.Should().NotContain("Before");
    }

    [Fact]
    public void EndDateFilter_ExcludesEntriesAfter()
    {
        _diaryService.SeedEntries(
            CreateEntry(new DateTime(2026, 3, 1, 10, 0, 0), 3, "Early"),
            CreateEntry(new DateTime(2026, 3, 10, 14, 0, 0), 4, "Middle"),
            CreateEntry(new DateTime(2026, 3, 20, 9, 0, 0), 5, "Late")
        );

        var cut = RenderExport();

        // Set end date
        typeof(Export).GetField("_endDate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(cut.Instance, (DateTime?)new DateTime(2026, 3, 15));

        // Click Export button
        var exportButton = cut.Find("button.mud-button-filled");
        exportButton.Click();

        // Should only show entries on or before March 15
        var rows = cut.FindAll(".print-table tbody tr");
        rows.Count.Should().Be(2);
        cut.Markup.Should().Contain("Early");
        cut.Markup.Should().Contain("Middle");
        cut.Markup.Should().NotContain("Late");
    }

    [Fact]
    public void BothFilters_CombinedWorksCorrectly()
    {
        _diaryService.SeedEntries(
            CreateEntry(new DateTime(2026, 3, 1, 10, 0, 0), 3, "Too early"),
            CreateEntry(new DateTime(2026, 3, 10, 14, 0, 0), 4, "In range"),
            CreateEntry(new DateTime(2026, 3, 15, 9, 0, 0), 5, "Also in range"),
            CreateEntry(new DateTime(2026, 3, 25, 9, 0, 0), 6, "Too late")
        );

        var cut = RenderExport();

        // Set both dates
        var exportType = typeof(Export);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        exportType.GetField("_startDate", flags)!.SetValue(cut.Instance, (DateTime?)new DateTime(2026, 3, 5));
        exportType.GetField("_endDate", flags)!.SetValue(cut.Instance, (DateTime?)new DateTime(2026, 3, 20));

        // Click Export button
        var exportButton = cut.Find("button.mud-button-filled");
        exportButton.Click();

        // Should only show entries within range
        var rows = cut.FindAll(".print-table tbody tr");
        rows.Count.Should().Be(2);
        cut.Markup.Should().Contain("In range");
        cut.Markup.Should().Contain("Also in range");
        cut.Markup.Should().NotContain("Too early");
        cut.Markup.Should().NotContain("Too late");
    }

    [Fact]
    public async Task ExportButton_CallsWindowPrint_ViaJSInterop()
    {
        // Set up JS interop handler for window.print
        _ctx.JSInterop.SetupVoid("window.print");

        _diaryService.SeedEntries(
            CreateEntry(new DateTime(2026, 3, 10, 10, 0, 0), 4, "Test entry")
        );

        var cut = RenderExport();

        // Click Export button
        var exportButton = cut.Find("button.mud-button-filled");
        exportButton.Click();

        // Wait for async PrintExport to complete (has Task.Delay(100))
        await Task.Delay(200);

        // Verify window.print was called via JS interop
        _ctx.JSInterop.VerifyInvoke("window.print");
    }

    [Fact]
    public void OnDataMerged_RefreshesEntries()
    {
        // Start with one entry
        _diaryService.SeedEntries(
            CreateEntry(new DateTime(2026, 3, 10, 10, 0, 0), 4, "Original")
        );

        var cut = RenderExport();
        cut.FindAll(".print-table tbody tr").Count.Should().Be(1);

        // Add another entry and fire OnDataMerged
        _diaryService.SeedEntries(
            CreateEntry(new DateTime(2026, 3, 15, 14, 0, 0), 5, "New from sync")
        );
        _syncService.RaiseOnDataMerged();

        // Wait for re-render
        cut.WaitForState(() => cut.FindAll(".print-table tbody tr").Count == 2);

        cut.FindAll(".print-table tbody tr").Count.Should().Be(2);
        cut.Markup.Should().Contain("New from sync");
    }
}
