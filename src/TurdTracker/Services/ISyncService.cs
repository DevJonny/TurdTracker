namespace TurdTracker.Services;

public enum SyncStatus
{
    NotSignedIn,
    Idle,
    Syncing,
    Synced,
    Error
}

public interface ISyncService
{
    Task SyncAsync();
    SyncStatus SyncStatus { get; }
    string? LastError { get; }
    DateTime? LastSyncedUtc { get; }
    event Action? OnSyncStatusChanged;
    Task InitializeAsync();
}
