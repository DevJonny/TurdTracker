using FluentAssertions;
using TurdTracker.Models;
using TurdTracker.Services;
using TurdTracker.Tests.Fakes;
using Xunit;

namespace TurdTracker.Tests.Services;

public class DiaryServiceTests
{
    private readonly FakeLocalStorageService _localStorage;
    private readonly DiaryService _sut;

    public DiaryServiceTests()
    {
        _localStorage = new FakeLocalStorageService();
        _sut = new DiaryService(_localStorage);
    }

    [Fact]
    public async Task AddAsync_StoresEntry_SetsLastModified_FiresOnDataChanged()
    {
        bool eventFired = false;
        _sut.OnDataChanged += () => eventFired = true;

        var entry = new DiaryEntry
        {
            Id = Guid.NewGuid(),
            BristolType = 4,
            Notes = "Test entry"
        };

        var before = DateTime.UtcNow;
        await _sut.AddAsync(entry);
        var after = DateTime.UtcNow;

        var all = await _sut.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].Id.Should().Be(entry.Id);
        all[0].LastModified.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        eventFired.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_FiltersOutDeletedEntries()
    {
        var active = new DiaryEntry { Id = Guid.NewGuid(), BristolType = 3 };
        var deleted = new DiaryEntry { Id = Guid.NewGuid(), BristolType = 5, IsDeleted = true };

        await _sut.AddAsync(active);
        await _sut.AddAsync(deleted);

        // Manually mark one as deleted via storage to bypass the service
        var all = await _sut.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].Id.Should().Be(active.Id);
    }

    [Fact]
    public async Task GetAllIncludingDeletedAsync_ReturnsAllEntries()
    {
        var active = new DiaryEntry { Id = Guid.NewGuid(), BristolType = 3 };
        await _sut.AddAsync(active);

        // Add a deleted entry directly
        var deleted = new DiaryEntry { Id = Guid.NewGuid(), BristolType = 5, IsDeleted = true };
        await _sut.AddAsync(deleted);

        var all = await _sut.GetAllIncludingDeletedAsync();
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectEntry()
    {
        var entry1 = new DiaryEntry { Id = Guid.NewGuid(), BristolType = 3, Notes = "first" };
        var entry2 = new DiaryEntry { Id = Guid.NewGuid(), BristolType = 5, Notes = "second" };
        await _sut.AddAsync(entry1);
        await _sut.AddAsync(entry2);

        var result = await _sut.GetByIdAsync(entry2.Id);
        result.Should().NotBeNull();
        result!.Notes.Should().Be("second");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByDateAsync_ReturnsEntriesMatchingDate()
    {
        var targetDate = new DateTime(2026, 3, 15, 10, 30, 0);
        var otherDate = new DateTime(2026, 3, 16, 8, 0, 0);

        var match = new DiaryEntry { Id = Guid.NewGuid(), Timestamp = targetDate, BristolType = 4 };
        var noMatch = new DiaryEntry { Id = Guid.NewGuid(), Timestamp = otherDate, BristolType = 2 };

        await _sut.AddAsync(match);
        await _sut.AddAsync(noMatch);

        var results = await _sut.GetByDateAsync(targetDate.Date);
        results.Should().HaveCount(1);
        results[0].Id.Should().Be(match.Id);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEntry_SetsLastModified_FiresOnDataChanged()
    {
        bool eventFired = false;
        var entry = new DiaryEntry { Id = Guid.NewGuid(), BristolType = 3, Notes = "original" };
        await _sut.AddAsync(entry);

        _sut.OnDataChanged += () => eventFired = true;

        entry.Notes = "updated";
        var before = DateTime.UtcNow;
        await _sut.UpdateAsync(entry);
        var after = DateTime.UtcNow;

        var result = await _sut.GetByIdAsync(entry.Id);
        result!.Notes.Should().Be("updated");
        result.LastModified.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        eventFired.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_NonExistentId_DoesNotFireOnDataChanged()
    {
        bool eventFired = false;
        _sut.OnDataChanged += () => eventFired = true;

        var entry = new DiaryEntry { Id = Guid.NewGuid(), BristolType = 3, Notes = "ghost" };
        await _sut.UpdateAsync(entry);

        eventFired.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_SetsIsDeleted_SetsLastModified_FiresOnDataChanged()
    {
        bool eventFired = false;
        var entry = new DiaryEntry { Id = Guid.NewGuid(), BristolType = 4 };
        await _sut.AddAsync(entry);

        _sut.OnDataChanged += () => eventFired = true;

        var before = DateTime.UtcNow;
        await _sut.DeleteAsync(entry.Id);
        var after = DateTime.UtcNow;

        // Should be filtered from GetAllAsync
        var active = await _sut.GetAllAsync();
        active.Should().BeEmpty();

        // But present in GetAllIncludingDeletedAsync
        var all = await _sut.GetAllIncludingDeletedAsync();
        all.Should().HaveCount(1);
        all[0].IsDeleted.Should().BeTrue();
        all[0].LastModified.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        eventFired.Should().BeTrue();
    }

    [Fact]
    public async Task ReplaceAllAsync_ReplacesAllEntries_DoesNotFireOnDataChanged()
    {
        bool eventFired = false;
        var original = new DiaryEntry { Id = Guid.NewGuid(), BristolType = 3 };
        await _sut.AddAsync(original);

        _sut.OnDataChanged += () => eventFired = true;

        var replacement = new DiaryEntry
        {
            Id = Guid.NewGuid(),
            BristolType = 6,
            LastModified = DateTime.UtcNow
        };
        await _sut.ReplaceAllAsync([replacement]);

        eventFired.Should().BeFalse();

        var all = await _sut.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].Id.Should().Be(replacement.Id);
    }

    [Fact]
    public async Task BackfillLastModifiedIfNeeded_MigratesDefaultDateTimeEntries()
    {
        // Seed entries directly into storage with default LastModified
        var timestamp = new DateTime(2026, 3, 10, 14, 30, 0, DateTimeKind.Local);
        var entry = new DiaryEntry
        {
            Id = Guid.NewGuid(),
            BristolType = 4,
            Timestamp = timestamp,
            LastModified = default(DateTime) // needs migration
        };

        await _localStorage.SetItemAsync("diary-entries", new List<DiaryEntry> { entry });

        // GetAllAsync triggers BackfillLastModifiedIfNeeded
        var result = await _sut.GetAllAsync();
        result.Should().HaveCount(1);
        result[0].LastModified.Should().Be(timestamp.ToUniversalTime());
    }
}
