using TurdTracker.Models;

namespace TurdTracker.Services;

public class SyncService : ISyncService, IDisposable
{
    private readonly IDiaryService _diaryService;
    private readonly IGoogleAuthService _authService;
    private readonly IGoogleDriveService _driveService;

    private CancellationTokenSource? _debounceCts;
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(2);
    private const int MaxRetries = 3;

    public SyncStatus SyncStatus { get; private set; } = SyncStatus.NotSignedIn;
    public DateTime? LastSyncedUtc { get; private set; }
    public event Action? OnSyncStatusChanged;

    public SyncService(
        IDiaryService diaryService,
        IGoogleAuthService authService,
        IGoogleDriveService driveService)
    {
        _diaryService = diaryService;
        _authService = authService;
        _driveService = driveService;

        _diaryService.OnDataChanged += OnDataChanged;
    }

    public async Task InitializeAsync()
    {
        await _authService.InitializeAsync();

        if (await _authService.IsSignedInAsync())
        {
            SetStatus(SyncStatus.Idle);
            await SyncAsync();
        }
        else if (await _authService.HasPreviousSessionAsync())
        {
            if (await _authService.TrySilentSignInAsync())
            {
                SetStatus(SyncStatus.Idle);
                await SyncAsync();
            }
            else
            {
                SetStatus(SyncStatus.NotSignedIn);
            }
        }
        else
        {
            SetStatus(SyncStatus.NotSignedIn);
        }
    }

    public async Task SyncAsync()
    {
        if (SyncStatus == SyncStatus.Syncing)
            return;

        if (!await _authService.IsSignedInAsync())
        {
            SetStatus(SyncStatus.NotSignedIn);
            return;
        }

        SetStatus(SyncStatus.Syncing);

        try
        {
            await SyncWithRetryAsync();
            LastSyncedUtc = DateTime.UtcNow;
            SetStatus(SyncStatus.Synced);
        }
        catch (HttpRequestException)
        {
            // Offline — skip silently, revert to previous good state
            SetStatus(LastSyncedUtc.HasValue ? SyncStatus.Synced : SyncStatus.Idle);
        }
        catch (Exception)
        {
            SetStatus(SyncStatus.Error);
        }
    }

    private async Task SyncWithRetryAsync()
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                await PerformSyncAsync();
                return;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
            {
                // 412 conflict — re-download and retry
                if (attempt == MaxRetries - 1)
                    throw;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // 401 — attempt silent re-auth
                var token = await _authService.SignInAsync();
                if (token == null)
                    throw;
                // Retry after re-auth
                if (attempt == MaxRetries - 1)
                    throw;
            }
        }
    }

    private async Task PerformSyncAsync()
    {
        // 1. Get local entries (including deleted for merge)
        var localEntries = await _diaryService.GetAllIncludingDeletedAsync();

        // 2. Download remote
        var (fileId, etag) = await _driveService.FindSyncFileAsync();

        SyncEnvelope? remoteEnvelope = null;
        if (fileId != null)
        {
            remoteEnvelope = await _driveService.DownloadSyncFileAsync(fileId);
        }

        var remoteEntries = remoteEnvelope?.Entries ?? [];

        // 3. Merge
        var result = MergeEngine.Merge(localEntries, remoteEntries);

        // 4. Save local if changed
        if (result.LocalChanged)
        {
            // Temporarily unsubscribe to avoid triggering a sync loop
            _diaryService.OnDataChanged -= OnDataChanged;
            try
            {
                await _diaryService.ReplaceAllAsync(result.MergedEntries);
            }
            finally
            {
                _diaryService.OnDataChanged += OnDataChanged;
            }
        }

        // 5. Upload if changed
        if (result.RemoteChanged)
        {
            var envelope = new SyncEnvelope
            {
                LastSyncedUtc = DateTime.UtcNow,
                Entries = result.MergedEntries
            };
            await _driveService.UploadSyncFileAsync(envelope, fileId, etag);
        }
    }

    private void OnDataChanged()
    {
        // Debounce: cancel any pending sync and schedule a new one
        var previousCts = _debounceCts;
        previousCts?.Cancel();
        previousCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceDelay, token);
                if (!token.IsCancellationRequested)
                {
                    await SyncAsync();
                }
            }
            catch (TaskCanceledException)
            {
                // Debounce cancelled — expected
            }
        });
    }

    private void SetStatus(SyncStatus status)
    {
        if (SyncStatus != status)
        {
            SyncStatus = status;
            OnSyncStatusChanged?.Invoke();
        }
    }

    public void Dispose()
    {
        _diaryService.OnDataChanged -= OnDataChanged;
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
    }
}
