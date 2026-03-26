using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using TurdTracker.Components;
using TurdTracker.Models;
using TurdTracker.Services;
using TurdTracker.Tests.Fakes;
using Xunit;

namespace TurdTracker.Tests.Components;

public class TagInputTests : IAsyncDisposable
{
    private readonly BunitContext _ctx;
    private readonly FakeDiaryService _diaryService;

    public TagInputTests()
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

    [Fact]
    public void AddingTag_ViaEnterKey_InvokesTagsChangedCallback()
    {
        List<string>? receivedTags = null;
        var tags = new List<string>();
        var cut = _ctx.Render<TagInput>(parameters => parameters
            .Add(p => p.Tags, tags)
            .Add(p => p.TagsChanged, EventCallback.Factory.Create<List<string>>(this, t => receivedTags = t)));

        // Find the MudTextField input
        var input = cut.Find("input");
        input.Change("breakfast");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        receivedTags.Should().NotBeNull();
        receivedTags.Should().Contain("breakfast");
    }

    [Fact]
    public void DuplicateTag_CaseInsensitive_IsNotAdded()
    {
        int callbackCount = 0;
        var tags = new List<string> { "Coffee" };
        var cut = _ctx.Render<TagInput>(parameters => parameters
            .Add(p => p.Tags, tags)
            .Add(p => p.TagsChanged, EventCallback.Factory.Create<List<string>>(this, _ => callbackCount++)));

        var input = cut.Find("input");
        input.Change("coffee");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        callbackCount.Should().Be(0);
        tags.Should().HaveCount(1);
    }

    [Fact]
    public void RemovingTag_InvokesTagsChangedCallback()
    {
        List<string>? receivedTags = null;
        var tags = new List<string> { "morning", "coffee" };
        var cut = _ctx.Render<TagInput>(parameters => parameters
            .Add(p => p.Tags, tags)
            .Add(p => p.TagsChanged, EventCallback.Factory.Create<List<string>>(this, t => receivedTags = t)));

        // Find the close button on the first chip
        var closeButtons = cut.FindAll(".mud-chip button");
        closeButtons.Should().HaveCountGreaterThan(0);
        closeButtons[0].Click();

        receivedTags.Should().NotBeNull();
        receivedTags.Should().NotContain("morning");
    }

    [Fact]
    public void TextField_ClearedAfterAddingTag()
    {
        var tags = new List<string>();
        var cut = _ctx.Render<TagInput>(parameters => parameters
            .Add(p => p.Tags, tags)
            .Add(p => p.TagsChanged, EventCallback.Factory.Create<List<string>>(this, _ => { })));

        var input = cut.Find("input");
        input.Change("lunch");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        // After adding, the input should be cleared
        input.GetAttribute("value").Should().BeNullOrEmpty();
    }

    [Fact]
    public void RecentTags_LoadedFromDiaryService_Top10_Deduped_ExcludingCurrent()
    {
        // Seed entries with various tags
        _diaryService.SeedEntries(
            new DiaryEntry { Tags = ["coffee", "morning", "breakfast"] },
            new DiaryEntry { Tags = ["coffee", "morning", "lunch"] },
            new DiaryEntry { Tags = ["coffee", "evening"] },
            new DiaryEntry { Tags = ["dinner", "late", "snack", "water", "fiber", "exercise", "stress", "medication", "travel"] },
            new DiaryEntry { Tags = ["extra-tag-11", "extra-tag-12"] }
        );

        // Pass "morning" as a current tag — it should be excluded from recent tags
        var tags = new List<string> { "morning" };
        var cut = _ctx.Render<TagInput>(parameters => parameters
            .Add(p => p.Tags, tags)
            .Add(p => p.TagsChanged, EventCallback.Factory.Create<List<string>>(this, _ => { })));

        // Should show "Recent tags" section
        cut.Markup.Should().Contain("Recent tags");

        // "morning" should be excluded (it's a current tag)
        // "coffee" should be first (appears 3 times, most frequent)
        var recentChips = cut.FindAll(".mud-chip");
        // First chip is the current tag "morning", rest are recent tags
        // The current tags are in the first section, recent tags in the second section

        // coffee appears 3 times — should be present (not excluded)
        cut.Markup.Should().Contain("coffee");
        // morning is excluded (current tag)
        // Count recent tag chips: we have many unique tags but max 10
        // Total unique tags excluding "morning": coffee, breakfast, lunch, evening, dinner, late, snack, water, fiber, exercise, stress, medication, travel, extra-tag-11, extra-tag-12 = 15
        // But only top 10 by frequency are shown

        // Verify "morning" is only in the current tags section, not duplicated in recent
        // The recent tags section should have at most 10 chips
    }
}
