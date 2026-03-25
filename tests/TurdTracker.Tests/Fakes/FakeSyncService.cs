using TurdTracker.Services;

namespace TurdTracker.Tests.Fakes;

public class FakeSyncService : ISyncService
{
    public SyncStatus SyncStatus { get; set; } = SyncStatus.NotSignedIn;
    public string? LastError { get; set; }
    public DateTime? LastSyncedUtc { get; set; }

    public event Action? OnSyncStatusChanged;
    public event Action? OnDataMerged;

    public List<string> MethodCalls { get; } = [];

    public Task InitializeAsync()
    {
        MethodCalls.Add(nameof(InitializeAsync));
        return Task.CompletedTask;
    }

    public Task SyncAsync()
    {
        MethodCalls.Add(nameof(SyncAsync));
        return Task.CompletedTask;
    }

    /// <summary>Fires OnSyncStatusChanged for testing subscriber behavior.</summary>
    public void RaiseOnSyncStatusChanged() => OnSyncStatusChanged?.Invoke();

    /// <summary>Fires OnDataMerged for testing subscriber behavior.</summary>
    public void RaiseOnDataMerged() => OnDataMerged?.Invoke();
}
