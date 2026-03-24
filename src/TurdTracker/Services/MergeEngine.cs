using TurdTracker.Models;

namespace TurdTracker.Services;

public class MergeResult
{
    public List<DiaryEntry> MergedEntries { get; set; } = [];
    public bool LocalChanged { get; set; }
    public bool RemoteChanged { get; set; }
}

public static class MergeEngine
{
    private static readonly TimeSpan TombstoneMaxAge = TimeSpan.FromDays(90);

    public static MergeResult Merge(List<DiaryEntry> local, List<DiaryEntry> remote)
    {
        var localById = local.ToDictionary(e => e.Id);
        var remoteById = remote.ToDictionary(e => e.Id);

        var allIds = new HashSet<Guid>(localById.Keys);
        allIds.UnionWith(remoteById.Keys);

        var merged = new List<DiaryEntry>();
        var localChanged = false;
        var remoteChanged = false;

        foreach (var id in allIds)
        {
            var inLocal = localById.TryGetValue(id, out var localEntry);
            var inRemote = remoteById.TryGetValue(id, out var remoteEntry);

            DiaryEntry winner;

            if (inLocal && inRemote)
            {
                // Both have this entry — latest LastModified wins
                if (localEntry!.LastModified >= remoteEntry!.LastModified)
                {
                    winner = localEntry;
                    if (localEntry.LastModified > remoteEntry.LastModified)
                    {
                        remoteChanged = true;
                    }
                }
                else
                {
                    winner = remoteEntry;
                    localChanged = true;
                }
            }
            else if (inLocal)
            {
                // Only in local — remote needs it
                winner = localEntry!;
                remoteChanged = true;
            }
            else
            {
                // Only in remote — local needs it
                winner = remoteEntry!;
                localChanged = true;
            }

            merged.Add(winner);
        }

        // Purge tombstones older than 90 days
        var now = DateTime.UtcNow;
        var purgedCount = merged.RemoveAll(e => e.IsDeleted && (now - e.LastModified) > TombstoneMaxAge);

        if (purgedCount > 0)
        {
            localChanged = true;
            remoteChanged = true;
        }

        return new MergeResult
        {
            MergedEntries = merged,
            LocalChanged = localChanged,
            RemoteChanged = remoteChanged
        };
    }
}
