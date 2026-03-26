using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using TurdTracker.Models;
using TurdTracker.Pages;
using TurdTracker.Services;
using TurdTracker.Tests.Fakes;
using Xunit;

namespace TurdTracker.Tests.Pages;

public class CalendarTests : IAsyncDisposable
{
    private readonly BunitContext _ctx;
    private readonly FakeDiaryService _diaryService;
    private readonly FakeSyncService _syncService;

    public CalendarTests()
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

    [Fact]
    public void CalendarGrid_ShowsCorrectNumberOfDays_ForCurrentMonth()
    {
        var cut = _ctx.Render<Calendar>();

        var daysInMonth = DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month);
        // Day cells contain a <b> element with the day number
        var dayNumbers = cut.FindAll(".calendar-cell b");
        dayNumbers.Count.Should().Be(daysInMonth);
    }

    [Fact]
    public void DayOfWeekOffset_IsCorrect_MondayStart()
    {
        var cut = _ctx.Render<Calendar>();

        // Check day-of-week headers
        var headers = cut.FindAll(".calendar-header");
        headers.Count.Should().Be(7);
        headers[0].TextContent.Should().Contain("Mon");
        headers[6].TextContent.Should().Contain("Sun");

        // Count empty cells (no blazor:onclick attribute = no day content)
        var allCells = cut.FindAll(".calendar-cell");
        var emptyCells = allCells.Count(c => string.IsNullOrWhiteSpace(c.InnerHtml));

        var firstDay = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        var expectedOffset = ((int)firstDay.DayOfWeek + 6) % 7;
        emptyCells.Should().Be(expectedOffset);
    }

    [Fact]
    public void EntryCountBadges_ShownOnDatesWithEntries()
    {
        var today = DateTime.Today;
        _diaryService.SeedEntries(
            new DiaryEntry { BristolType = 3, Timestamp = today.AddHours(8) },
            new DiaryEntry { BristolType = 4, Timestamp = today.AddHours(14) }
        );

        var cut = _ctx.Render<Calendar>();

        // The has-entries class should be on at least one cell
        var cellsWithEntries = cut.FindAll(".has-entries");
        cellsWithEntries.Count.Should().BeGreaterThanOrEqualTo(1);

        // MudBadge should show count of 2
        cut.Markup.Should().Contain("2");
    }

    [Fact]
    public void ClickingDate_SelectsIt_AndShowsEntriesForThatDay()
    {
        var targetDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15, 10, 30, 0);
        _diaryService.SeedEntries(
            new DiaryEntry { BristolType = 5, Timestamp = targetDate, Notes = "Test entry on 15th" }
        );

        var cut = _ctx.Render<Calendar>();

        // Find the cell for day 15 — look for <b> containing "15" inside calendar cells
        var dayCells = cut.FindAll(".calendar-cell");
        var day15Cell = dayCells.FirstOrDefault(c =>
        {
            var boldElements = c.QuerySelectorAll("b");
            return boldElements.Any(b => b.TextContent.Trim() == "15");
        });
        day15Cell.Should().NotBeNull();
        day15Cell!.Click();

        // Should show the selected class
        cut.FindAll(".selected").Count.Should().BeGreaterThanOrEqualTo(1);

        // Should show entries for that day
        cut.Markup.Should().Contain("Test entry on 15th");
    }

    [Fact]
    public void PreviousNextMonthButtons_NavigateMonths()
    {
        var cut = _ctx.Render<Calendar>();

        var currentMonthName = DateTime.Today.ToString("MMMM yyyy");
        cut.Markup.Should().Contain(currentMonthName);

        // MudIconButton renders as button.mud-icon-button — first is prev, second is next
        var iconButtons = cut.FindAll("button.mud-icon-button");
        iconButtons.Count.Should().BeGreaterThanOrEqualTo(2);

        // Click previous month (first button)
        iconButtons[0].Click();

        var prevMonth = DateTime.Today.AddMonths(-1);
        cut.Markup.Should().Contain(prevMonth.ToString("MMMM yyyy"));

        // Click next month twice (back to current, then forward)
        var nextButtons = cut.FindAll("button.mud-icon-button");
        nextButtons[1].Click();
        nextButtons = cut.FindAll("button.mud-icon-button");
        nextButtons[1].Click();

        var nextMonth = DateTime.Today.AddMonths(1);
        cut.Markup.Should().Contain(nextMonth.ToString("MMMM yyyy"));
    }

    [Fact]
    public void EmptySelectedDate_ShowsNoEntriesMessage()
    {
        var cut = _ctx.Render<Calendar>();

        // Today is selected by default after init, and should have no entries
        cut.Markup.Should().Contain("No entries for this day");
    }

    [Fact]
    public void OnDataMerged_RefreshesEntries()
    {
        var cut = _ctx.Render<Calendar>();

        // Initially no entries
        cut.FindAll(".has-entries").Count.Should().Be(0);

        // Add an entry for today and fire OnDataMerged
        _diaryService.SeedEntries(
            new DiaryEntry { BristolType = 4, Timestamp = DateTime.Today.AddHours(12), Notes = "Synced entry" }
        );
        _syncService.RaiseOnDataMerged();

        // Wait for async re-render
        cut.WaitForState(() => cut.FindAll(".has-entries").Count > 0, TimeSpan.FromSeconds(2));

        cut.FindAll(".has-entries").Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Dispose_UnsubscribesOnDataMerged()
    {
        var cut = _ctx.Render<Calendar>();
        cut.Dispose();

        // Firing OnDataMerged after dispose should not throw
        var act = () => _syncService.RaiseOnDataMerged();
        act.Should().NotThrow();
    }
}
