using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using TurdTracker.Pages;
using TurdTracker.Services;
using TurdTracker.Tests.Fakes;
using Xunit;

namespace TurdTracker.Tests.Pages;

public class LogTests : IAsyncDisposable
{
    private readonly BunitContext _ctx;
    private readonly FakeDiaryService _diaryService;

    public LogTests()
    {
        _ctx = new BunitContext();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();

        _diaryService = new FakeDiaryService();
        _ctx.Services.AddSingleton<IDiaryService>(_diaryService);
    }

    public async ValueTask DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    private IRenderedComponent<Log> RenderLog()
    {
        // MudDatePicker/MudTimePicker require MudPopoverProvider in the render tree
        var cut = _ctx.Render<MudPopoverProvider>();
        return _ctx.Render<Log>();
    }

    [Fact]
    public void Save_WithBristolTypeZero_ShowsValidationError_DoesNotCallAddAsync()
    {
        var cut = RenderLog();

        // Find and click the Save button
        var saveButton = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Save"));
        saveButton.Click();

        // Validation error should be shown
        cut.Markup.Should().Contain("Please select a Bristol type");

        // AddAsync should NOT have been called
        _diaryService.MethodCalls.Should().NotContain("AddAsync");
    }

    [Fact]
    public async Task Save_WithValidData_CallsAddAsync_AndNavigatesToHome()
    {
        var cut = RenderLog();
        var nav = _ctx.Services.GetRequiredService<NavigationManager>();

        // Select Bristol type by clicking a card (type 3)
        var bristolCards = cut.FindAll(".bristol-card");
        bristolCards[2].Click(); // 0-indexed, so index 2 = type 3

        // Click Save
        var saveButton = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Save"));
        saveButton.Click();

        // Wait for async save to complete
        cut.WaitForState(() => _diaryService.MethodCalls.Contains("AddAsync"));

        // Verify AddAsync was called
        _diaryService.MethodCalls.Should().Contain("AddAsync");

        // Verify entry was stored with correct Bristol type
        var entries = await _diaryService.GetAllAsync();
        entries.Should().HaveCount(1);
        entries[0].BristolType.Should().Be(3);

        // Verify navigation to home
        nav.Uri.Should().EndWith("/");
    }

    [Fact]
    public async Task Save_EntryTimestamp_CombinesSelectedDateAndTime()
    {
        var cut = RenderLog();

        // Select Bristol type (required for save)
        var bristolCards = cut.FindAll(".bristol-card");
        bristolCards[0].Click(); // type 1

        // Click Save (uses default date/time which is today + now)
        var saveButton = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Save"));
        saveButton.Click();

        cut.WaitForState(() => _diaryService.MethodCalls.Contains("AddAsync"));

        var entries = await _diaryService.GetAllAsync();
        entries.Should().HaveCount(1);

        // Timestamp should be today's date combined with a time component
        var entry = entries[0];
        entry.Timestamp.Date.Should().Be(DateTime.Today);
        // Time component should be non-zero (defaults to DateTime.Now.TimeOfDay)
        entry.Timestamp.TimeOfDay.Should().NotBe(TimeSpan.Zero);
    }

    [Fact]
    public void Cancel_NavigatesToHome()
    {
        var cut = RenderLog();
        var nav = _ctx.Services.GetRequiredService<NavigationManager>();

        // Find and click Cancel button
        var cancelButton = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Cancel"));
        cancelButton.Click();

        // Should navigate to home
        nav.Uri.Should().EndWith("/");
    }
}
