using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using MudBlazor.Services;
using TurdTracker.Components;
using Xunit;

namespace TurdTracker.Tests.Components;

public class BristolScaleSelectorTests : IDisposable
{
    private readonly BunitContext _ctx;

    public BristolScaleSelectorTests()
    {
        _ctx = new BunitContext();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();
    }

    public void Dispose()
    {
        _ctx.Dispose();
    }

    [Fact]
    public void RendersAll7BristolTypeCards()
    {
        var cut = _ctx.Render<BristolScaleSelector>(parameters => parameters
            .Add(p => p.SelectedType, 0));

        var typeNames = new[]
        {
            "Separate hard lumps",
            "Lumpy sausage",
            "Cracked sausage",
            "Smooth sausage",
            "Soft blobs",
            "Fluffy pieces",
            "Watery"
        };

        foreach (var name in typeNames)
        {
            cut.Markup.Should().Contain(name);
        }

        // 7 type labels "Type 1" through "Type 7"
        for (int i = 1; i <= 7; i++)
        {
            cut.Markup.Should().Contain($"Type {i}");
        }
    }

    [Fact]
    public void SelectedTypeCard_HasSelectedClass()
    {
        var cut = _ctx.Render<BristolScaleSelector>(parameters => parameters
            .Add(p => p.SelectedType, 3));

        // Find all MudPaper elements (the cards)
        var papers = cut.FindAll(".bristol-card");
        papers.Should().HaveCount(7);

        // The 3rd card (index 2) should have the selected class
        papers[2].ClassList.Should().Contain("bristol-card-selected");

        // Other cards should not have the selected class
        for (int i = 0; i < 7; i++)
        {
            if (i == 2) continue;
            papers[i].ClassList.Should().NotContain("bristol-card-selected");
        }
    }

    [Fact]
    public void ClickingCard_InvokesSelectedTypeChanged_WithCorrectValue()
    {
        int selectedValue = 0;
        var cut = _ctx.Render<BristolScaleSelector>(parameters => parameters
            .Add(p => p.SelectedType, 0)
            .Add(p => p.SelectedTypeChanged, EventCallback.Factory.Create<int>(this, v => selectedValue = v)));

        // Click the 5th card (index 4)
        var papers = cut.FindAll(".bristol-card");
        papers[4].Click();

        selectedValue.Should().Be(5);
    }

    [Fact]
    public void NoCardSelected_WhenSelectedTypeIsZero()
    {
        var cut = _ctx.Render<BristolScaleSelector>(parameters => parameters
            .Add(p => p.SelectedType, 0));

        var papers = cut.FindAll(".bristol-card");
        papers.Should().HaveCount(7);

        // No card should have the selected class
        foreach (var paper in papers)
        {
            paper.ClassList.Should().NotContain("bristol-card-selected");
        }
    }
}
