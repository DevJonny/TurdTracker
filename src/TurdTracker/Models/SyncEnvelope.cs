namespace TurdTracker.Models;

public class SyncEnvelope
{
    public string Version { get; set; } = "1";
    public DateTime LastSyncedUtc { get; set; }
    public List<DiaryEntry> Entries { get; set; } = [];
}
