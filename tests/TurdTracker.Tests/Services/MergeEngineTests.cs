using FluentAssertions;
using TurdTracker.Models;
using TurdTracker.Services;
using Xunit;

namespace TurdTracker.Tests.Services;

public class MergeEngineTests
{
    private static DiaryEntry CreateEntry(Guid? id = null, DateTime? lastModified = null, bool isDeleted = false)
    {
        return new DiaryEntry
        {
            Id = id ?? Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            BristolType = 4,
            Notes = "test",
            LastModified = lastModified ?? DateTime.UtcNow,
            IsDeleted = isDeleted
        };
    }

    [Fact]
    public void Merge_EmptyLocalAndRemote_ReturnsEmptyWithNoChanges()
    {
        var result = MergeEngine.Merge([], []);

        result.MergedEntries.Should().BeEmpty();
        result.LocalChanged.Should().BeFalse();
        result.RemoteChanged.Should().BeFalse();
    }

    [Fact]
    public void Merge_LocalOnlyEntries_ReturnedWithRemoteChanged()
    {
        var entry = CreateEntry();
        var result = MergeEngine.Merge([entry], []);

        result.MergedEntries.Should().ContainSingle().Which.Should().BeSameAs(entry);
        result.RemoteChanged.Should().BeTrue();
        result.LocalChanged.Should().BeFalse();
    }

    [Fact]
    public void Merge_RemoteOnlyEntries_ReturnedWithLocalChanged()
    {
        var entry = CreateEntry();
        var result = MergeEngine.Merge([], [entry]);

        result.MergedEntries.Should().ContainSingle().Which.Should().BeSameAs(entry);
        result.LocalChanged.Should().BeTrue();
        result.RemoteChanged.Should().BeFalse();
    }

    [Fact]
    public void Merge_BothHaveSameEntry_LocalNewer_LocalWins()
    {
        var id = Guid.NewGuid();
        var localEntry = CreateEntry(id, DateTime.UtcNow);
        var remoteEntry = CreateEntry(id, DateTime.UtcNow.AddMinutes(-10));

        var result = MergeEngine.Merge([localEntry], [remoteEntry]);

        result.MergedEntries.Should().ContainSingle().Which.Should().BeSameAs(localEntry);
        result.RemoteChanged.Should().BeTrue();
        result.LocalChanged.Should().BeFalse();
    }

    [Fact]
    public void Merge_BothHaveSameEntry_RemoteNewer_RemoteWins()
    {
        var id = Guid.NewGuid();
        var localEntry = CreateEntry(id, DateTime.UtcNow.AddMinutes(-10));
        var remoteEntry = CreateEntry(id, DateTime.UtcNow);

        var result = MergeEngine.Merge([localEntry], [remoteEntry]);

        result.MergedEntries.Should().ContainSingle().Which.Should().BeSameAs(remoteEntry);
        result.LocalChanged.Should().BeTrue();
        result.RemoteChanged.Should().BeFalse();
    }

    [Fact]
    public void Merge_BothHaveSameEntry_EqualLastModified_LocalWinsTieBreak()
    {
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var localEntry = CreateEntry(id, timestamp);
        var remoteEntry = CreateEntry(id, timestamp);

        var result = MergeEngine.Merge([localEntry], [remoteEntry]);

        result.MergedEntries.Should().ContainSingle().Which.Should().BeSameAs(localEntry);
        // Equal timestamps: local wins but remoteChanged is NOT set (no actual difference)
        result.RemoteChanged.Should().BeFalse();
        result.LocalChanged.Should().BeFalse();
    }

    [Fact]
    public void Merge_SoftDeletedOlderThan90Days_PurgedFromResult()
    {
        var entry = CreateEntry(
            lastModified: DateTime.UtcNow.AddDays(-91),
            isDeleted: true);

        var result = MergeEngine.Merge([entry], []);

        result.MergedEntries.Should().BeEmpty();
        result.LocalChanged.Should().BeTrue();
        result.RemoteChanged.Should().BeTrue();
    }

    [Fact]
    public void Merge_SoftDeletedYoungerThan90Days_KeptInResult()
    {
        var entry = CreateEntry(
            lastModified: DateTime.UtcNow.AddDays(-30),
            isDeleted: true);

        var result = MergeEngine.Merge([entry], []);

        result.MergedEntries.Should().ContainSingle().Which.Should().BeSameAs(entry);
    }

    [Fact]
    public void Merge_MixOfLocalRemoteAndConflicts_AllResolvedCorrectly()
    {
        var conflictId = Guid.NewGuid();
        var localOnly = CreateEntry();
        var remoteOnly = CreateEntry();
        var localConflict = CreateEntry(conflictId, DateTime.UtcNow);
        var remoteConflict = CreateEntry(conflictId, DateTime.UtcNow.AddMinutes(-5));

        var result = MergeEngine.Merge(
            [localOnly, localConflict],
            [remoteOnly, remoteConflict]);

        result.MergedEntries.Should().HaveCount(3);
        result.MergedEntries.Should().Contain(localOnly);
        result.MergedEntries.Should().Contain(remoteOnly);
        result.MergedEntries.Should().Contain(localConflict);
        result.MergedEntries.Should().NotContain(remoteConflict);
        result.LocalChanged.Should().BeTrue();   // remote-only entry
        result.RemoteChanged.Should().BeTrue();  // local-only + conflict won by local
    }
}
