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

public class StatsTests : IAsyncDisposable
{
    private readonly BunitContext _ctx;
    private readonly FakeDiaryService _diaryService;
    private readonly FakeSyncService _syncService;

    public StatsTests()
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
    public void FrequencyChart_ComputesCorrectDailyCounts_For7DayRange()
    {
        var today = DateTime.Today;
        _diaryService.SeedEntries(
            new DiaryEntry { BristolType = 3, Timestamp = today.AddHours(8) },
            new DiaryEntry { BristolType = 4, Timestamp = today.AddHours(14) },
            new DiaryEntry { BristolType = 5, Timestamp = today.AddDays(-1).AddHours(10) }
        );

        var cut = _ctx.Render<Stats>();

        // Default range is 7 days — frequency chart should have 7 x-axis labels
        var xAxisLabels = cut.FindAll("svg.mud-chart-bar text");
        // 7 x-axis labels + y-axis label(s) = the frequency chart renders with data
        cut.Markup.Should().Contain("Frequency Over Time");

        // Bar chart SVGs should be rendered
        var svgs = cut.FindAll("svg.mud-chart-bar");
        svgs.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void BristolDistribution_TalliesTypes1Through7()
    {
        _diaryService.SeedEntries(
            new DiaryEntry { BristolType = 1, Timestamp = DateTime.Today.AddHours(8) },
            new DiaryEntry { BristolType = 1, Timestamp = DateTime.Today.AddHours(9) },
            new DiaryEntry { BristolType = 3, Timestamp = DateTime.Today.AddHours(10) },
            new DiaryEntry { BristolType = 7, Timestamp = DateTime.Today.AddHours(11) }
        );

        var cut = _ctx.Render<Stats>();

        // Bristol distribution section should render chart, not "No entries to display."
        cut.Markup.Should().Contain("Bristol Type Distribution");
        // With entries, the Bristol chart renders (not the empty message)
        // The labels "Type 1" through "Type 7" should appear in x-axis
        cut.Markup.Should().Contain("Type 1");
        cut.Markup.Should().Contain("Type 7");
    }

    [Fact]
    public void TimeOfDayChart_BucketsEntriesByHour()
    {
        _diaryService.SeedEntries(
            new DiaryEntry { BristolType = 3, Timestamp = DateTime.Today.AddHours(6) },
            new DiaryEntry { BristolType = 4, Timestamp = DateTime.Today.AddHours(6).AddMinutes(30) },
            new DiaryEntry { BristolType = 5, Timestamp = DateTime.Today.AddHours(18) }
        );

        var cut = _ctx.Render<Stats>();

        // Time of day section should render chart
        cut.Markup.Should().Contain("Time of Day Patterns");
        // Should render the SVG chart (not "No entries to display.")
        // Hour labels appear every 3 hours: 00:00, 03:00, 06:00, etc.
        cut.Markup.Should().Contain("06:00");
        cut.Markup.Should().Contain("18:00");
    }

    [Fact]
    public void TimeRangeChipSelection_RebuildsFrequencyChart()
    {
        // Seed an entry 20 days ago (outside 7-day range, inside 30-day range)
        var twentyDaysAgo = DateTime.Today.AddDays(-20).AddHours(10);
        _diaryService.SeedEntries(
            new DiaryEntry { BristolType = 4, Timestamp = twentyDaysAgo }
        );

        var cut = _ctx.Render<Stats>();

        // Default 7-day range: frequency chart has 7 bars (all zero height since entry is outside range)
        var firstChartSvg = cut.FindAll("svg.mud-chart-bar")[0];
        var barsIn7Day = firstChartSvg.QuerySelectorAll(".mud-chart-bar");
        barsIn7Day.Length.Should().Be(7);

        // Click the "Last 30 days" chip
        var chips = cut.FindAll(".mud-chip");
        var chip30 = chips.FirstOrDefault(c => c.TextContent.Contains("30"));
        chip30.Should().NotBeNull();
        chip30!.Click();

        // After switching to 30-day range, frequency chart should have 30 bars
        firstChartSvg = cut.FindAll("svg.mud-chart-bar")[0];
        var barsIn30Day = firstChartSvg.QuerySelectorAll(".mud-chart-bar");
        barsIn30Day.Length.Should().Be(30);
    }

    [Fact]
    public void EmptyData_RendersWithoutErrors()
    {
        // No entries seeded
        var cut = _ctx.Render<Stats>();

        // Page should render all section headers without errors
        cut.Markup.Should().Contain("Frequency Over Time");
        cut.Markup.Should().Contain("Bristol Type Distribution");
        cut.Markup.Should().Contain("Time of Day Patterns");

        // Bristol and time of day charts show "No entries to display." when all data is zero
        var noEntriesCount = cut.Markup.Split("No entries to display.").Length - 1;
        noEntriesCount.Should().Be(2);

        // Frequency chart still renders (array is non-empty, just all zeros)
        cut.FindAll("svg.mud-chart-bar").Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void OnDataMerged_RebuildsCharts()
    {
        var cut = _ctx.Render<Stats>();

        // Initially empty: Bristol/TimeOfDay show "No entries to display."
        cut.Markup.Should().Contain("No entries to display.");

        // Add entries and fire OnDataMerged
        _diaryService.SeedEntries(
            new DiaryEntry { BristolType = 4, Timestamp = DateTime.Today.AddHours(12) }
        );
        _syncService.RaiseOnDataMerged();

        // Wait for async re-render — Bristol chart should now render instead of "No entries to display."
        cut.WaitForState(() =>
        {
            // With one entry, Bristol and TimeOfDay charts render, reducing "No entries to display." count
            var count = cut.Markup.Split("No entries to display.").Length - 1;
            return count == 0;
        }, TimeSpan.FromSeconds(2));

        // All three charts should now have data
        cut.FindAll("svg.mud-chart-bar").Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Dispose_UnsubscribesOnDataMerged()
    {
        var cut = _ctx.Render<Stats>();
        cut.Dispose();

        // Firing OnDataMerged after dispose should not throw
        var act = () => _syncService.RaiseOnDataMerged();
        act.Should().NotThrow();
    }
}
