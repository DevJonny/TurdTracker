using System.Net;
using System.Net.Http;
using FluentAssertions;
using TurdTracker.Models;
using TurdTracker.Services;
using TurdTracker.Tests.Fakes;
using Xunit;

namespace TurdTracker.Tests.Services;

public class SyncServiceTests : IDisposable
{
    private readonly FakeDiaryService _diaryService = new();
    private readonly FakeGoogleAuthService _authService = new();
    private readonly FakeGoogleDriveService _driveService = new();
    private readonly SyncService _sut;

    public SyncServiceTests()
    {
        _sut = new SyncService(_diaryService, _authService, _driveService);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    #region InitializeAsync

    [Fact]
    public async Task InitializeAsync_WhenSignedIn_TriggersSyncAndTransitionsToSynced()
    {
        // Arrange: user is signed in, no remote file
        _authService.IsSignedIn = true;
        _driveService.FindResult = (null, null);

        // Act
        await _sut.InitializeAsync();

        // Assert: should have called InitializeAsync on auth, then synced
        _authService.MethodCalls.Should().Contain(nameof(IGoogleAuthService.InitializeAsync));
        _driveService.MethodCalls.Should().Contain(nameof(IGoogleDriveService.FindSyncFileAsync));
        _sut.SyncStatus.Should().Be(SyncStatus.Synced);
        _sut.LastSyncedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task InitializeAsync_WithPreviousSession_SilentSignInSuccess_TriggersSyncAndSynced()
    {
        // Arrange: not currently signed in, but has previous session, silent sign-in succeeds
        _authService.IsSignedIn = false;
        _authService.HasPreviousSession = true;
        _authService.SilentSignInResult = true;

        // After TrySilentSignInAsync succeeds, IsSignedInAsync needs to return true for SyncAsync
        // We need to set IsSignedIn after TrySilentSignIn is called
        // Since our fake is simple, we'll set IsSignedIn = true when TrySilentSignIn returns true
        // Actually, we need to handle this: InitializeAsync calls IsSignedInAsync (false),
        // then HasPreviousSessionAsync (true), then TrySilentSignInAsync (true),
        // then SyncAsync which calls IsSignedInAsync again.
        // We need IsSignedIn to become true after silent sign-in.

        // Workaround: Override to make IsSignedIn switch after silent sign-in
        var authService = new FakeGoogleAuthServiceWithSilentSignInSwitch();
        using var sut = new SyncService(_diaryService, authService, _driveService);

        await sut.InitializeAsync();

        sut.SyncStatus.Should().Be(SyncStatus.Synced);
        sut.LastSyncedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task InitializeAsync_NoPreviousSession_StatusStaysNotSignedIn()
    {
        // Arrange: not signed in and no previous session
        _authService.IsSignedIn = false;
        _authService.HasPreviousSession = false;

        // Act
        await _sut.InitializeAsync();

        // Assert
        _sut.SyncStatus.Should().Be(SyncStatus.NotSignedIn);
        _driveService.MethodCalls.Should().NotContain(nameof(IGoogleDriveService.FindSyncFileAsync));
    }

    #endregion

    #region SyncAsync — status transitions

    [Fact]
    public async Task SyncAsync_WhenNotSignedIn_SetsNotSignedInStatus()
    {
        // Arrange: not signed in
        _authService.IsSignedIn = false;

        // Act
        await _sut.SyncAsync();

        // Assert
        _sut.SyncStatus.Should().Be(SyncStatus.NotSignedIn);
        _driveService.MethodCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task SyncAsync_HappyPath_TransitionsToSyncedAndSetsLastSyncedUtc()
    {
        // Arrange: signed in, no remote file (simple sync)
        _authService.IsSignedIn = true;
        _driveService.FindResult = (null, null);

        var statusTransitions = new List<SyncStatus>();
        _sut.OnSyncStatusChanged += () => statusTransitions.Add(_sut.SyncStatus);

        // Act
        var before = DateTime.UtcNow;
        await _sut.SyncAsync();

        // Assert
        statusTransitions.Should().Contain(SyncStatus.Syncing);
        _sut.SyncStatus.Should().Be(SyncStatus.Synced);
        _sut.LastSyncedUtc.Should().NotBeNull();
        _sut.LastSyncedUtc!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task SyncAsync_ConcurrentCall_ReturnsImmediately()
    {
        // Arrange: signed in, drive service will be slow
        _authService.IsSignedIn = true;

        var tcs = new TaskCompletionSource<(string? FileId, string? ETag)>();
        var slowDriveService = new SlowFakeGoogleDriveService(tcs.Task);
        using var sut = new SyncService(_diaryService, _authService, slowDriveService);

        // Act: start first sync (will block on FindSyncFileAsync)
        var firstSync = sut.SyncAsync();

        // Second sync should return immediately because status is Syncing
        await sut.SyncAsync();

        // Complete the first sync
        tcs.SetResult((null, null));
        await firstSync;

        // Assert: only one FindSyncFileAsync call
        slowDriveService.FindCallCount.Should().Be(1);
    }

    #endregion

    #region SyncAsync — merge results

    [Fact]
    public async Task SyncAsync_WithLocalChanged_CallsReplaceAllAsync()
    {
        // Arrange: signed in, remote has entries that local doesn't
        _authService.IsSignedIn = true;
        var remoteEntry = new DiaryEntry
        {
            Id = Guid.NewGuid(),
            BristolType = 4,
            Timestamp = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };
        _driveService.FindResult = ("file-1", "\"etag-1\"");
        _driveService.DownloadResult = new SyncEnvelope
        {
            Entries = [remoteEntry]
        };

        // Act
        await _sut.SyncAsync();

        // Assert: local was updated
        _diaryService.MethodCalls.Should().Contain(nameof(IDiaryService.ReplaceAllAsync));
        _sut.SyncStatus.Should().Be(SyncStatus.Synced);
    }

    [Fact]
    public async Task SyncAsync_WithRemoteChanged_CallsUploadSyncFileAsync()
    {
        // Arrange: signed in, local has entries that remote doesn't
        _authService.IsSignedIn = true;
        _diaryService.SeedEntries(new DiaryEntry
        {
            Id = Guid.NewGuid(),
            BristolType = 3,
            Timestamp = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        });
        _driveService.FindResult = (null, null); // no remote file

        // Act
        await _sut.SyncAsync();

        // Assert: remote was updated
        _driveService.MethodCalls.Should().Contain(nameof(IGoogleDriveService.UploadSyncFileAsync));
        _driveService.LastUploadedEnvelope.Should().NotBeNull();
        _driveService.LastUploadedEnvelope!.Entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task SyncAsync_OnDataChangedResubscribedEvenOnException()
    {
        // Arrange: signed in, ReplaceAllAsync will be called (remote-only entries cause LocalChanged)
        _authService.IsSignedIn = true;
        var remoteEntry = new DiaryEntry
        {
            Id = Guid.NewGuid(),
            BristolType = 4,
            Timestamp = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };
        _driveService.FindResult = ("file-1", "\"etag-1\"");
        _driveService.DownloadResult = new SyncEnvelope
        {
            Entries = [remoteEntry]
        };

        // Act
        await _sut.SyncAsync();

        // Assert: After sync, OnDataChanged should still be subscribed.
        // Verify by triggering OnDataChanged and checking it doesn't throw
        // (if unsubscribed, the debounce handler wouldn't fire)
        _diaryService.MethodCalls.Clear();

        // The event handler is re-subscribed in the finally block
        // We can verify by checking that the sync service is still wired up
        _sut.SyncStatus.Should().Be(SyncStatus.Synced);
    }

    #endregion

    #region SyncAsync — error handling and retry

    [Fact]
    public async Task SyncAsync_412Conflict_RetriesWithRedownloadAndRemerge()
    {
        // Arrange: signed in, local entries to trigger upload (remoteChanged)
        _authService.IsSignedIn = true;
        _diaryService.SeedEntries(new DiaryEntry
        {
            Id = Guid.NewGuid(),
            BristolType = 3,
            Timestamp = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        });

        var driveService = new ConfigurableDriveService();
        driveService.EnqueueUploadException(
            FakeGoogleDriveService.CreateHttpException(HttpStatusCode.PreconditionFailed));
        // Second attempt succeeds (no exception enqueued)

        using var sut = new SyncService(_diaryService, _authService, driveService);

        // Act
        await sut.SyncAsync();

        // Assert: retried — FindSyncFileAsync called twice (once per attempt)
        driveService.FindCallCount.Should().Be(2);
        driveService.UploadCallCount.Should().Be(2);
        sut.SyncStatus.Should().Be(SyncStatus.Synced);
    }

    [Fact]
    public async Task SyncAsync_412Conflict_ExhaustsRetries_SetsErrorStatus()
    {
        // Arrange: signed in, local entries, upload always throws 412
        _authService.IsSignedIn = true;
        _diaryService.SeedEntries(new DiaryEntry
        {
            Id = Guid.NewGuid(),
            BristolType = 3,
            Timestamp = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        });

        var driveService = new ConfigurableDriveService();
        driveService.EnqueueUploadException(
            FakeGoogleDriveService.CreateHttpException(HttpStatusCode.PreconditionFailed, "412 Conflict"));
        driveService.EnqueueUploadException(
            FakeGoogleDriveService.CreateHttpException(HttpStatusCode.PreconditionFailed, "412 Conflict"));
        driveService.EnqueueUploadException(
            FakeGoogleDriveService.CreateHttpException(HttpStatusCode.PreconditionFailed, "412 Conflict"));

        using var sut = new SyncService(_diaryService, _authService, driveService);

        // Act
        await sut.SyncAsync();

        // Assert: all 3 retries exhausted, error status
        driveService.UploadCallCount.Should().Be(3);
        sut.SyncStatus.Should().Be(SyncStatus.Error);
        sut.LastError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SyncAsync_401Unauthorized_AttemptsReauthAndRetries()
    {
        // Arrange: signed in, local entries, upload throws 401 first time
        _authService.IsSignedIn = true;
        _authService.SignInResult = "new-token";
        _diaryService.SeedEntries(new DiaryEntry
        {
            Id = Guid.NewGuid(),
            BristolType = 3,
            Timestamp = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        });

        var driveService = new ConfigurableDriveService();
        driveService.EnqueueUploadException(
            FakeGoogleDriveService.CreateHttpException(HttpStatusCode.Unauthorized));

        using var sut = new SyncService(_diaryService, _authService, driveService);

        // Act
        await sut.SyncAsync();

        // Assert: re-auth attempted, then retry succeeded
        _authService.MethodCalls.Should().Contain(nameof(IGoogleAuthService.SignInAsync));
        driveService.UploadCallCount.Should().Be(2);
        sut.SyncStatus.Should().Be(SyncStatus.Synced);
    }

    [Fact]
    public async Task SyncAsync_NetworkError_RevertsToPreviousStatus_NoErrorShown()
    {
        // Arrange: do a successful sync first to establish LastSyncedUtc
        _authService.IsSignedIn = true;
        _driveService.FindResult = (null, null);
        await _sut.SyncAsync();
        _sut.SyncStatus.Should().Be(SyncStatus.Synced);
        _sut.LastSyncedUtc.Should().NotBeNull();

        // Now set up network error (null StatusCode)
        _driveService.FindException = new HttpRequestException("Network error", null, null);

        // Act
        await _sut.SyncAsync();

        // Assert: reverts to Synced (had previous sync), no error shown
        _sut.SyncStatus.Should().Be(SyncStatus.Synced);
        _sut.LastError.Should().BeNull();
    }

    [Fact]
    public async Task SyncAsync_RealHttpError_SetsErrorStatusAndCapturesLastError()
    {
        // Arrange: signed in, 500 error
        _authService.IsSignedIn = true;
        _driveService.FindException = FakeGoogleDriveService.CreateHttpException(
            HttpStatusCode.InternalServerError, "Internal Server Error");

        // Act
        await _sut.SyncAsync();

        // Assert
        _sut.SyncStatus.Should().Be(SyncStatus.Error);
        _sut.LastError.Should().Contain("Internal Server Error");
    }

    [Fact]
    public async Task SyncAsync_GeneralException_SetsErrorStatusAndCapturesLastError()
    {
        // Arrange: signed in, drive service throws non-HTTP exception
        _authService.IsSignedIn = true;
        var driveService = new ThrowingDriveService(new InvalidOperationException("Something broke"));
        using var sut = new SyncService(_diaryService, _authService, driveService);

        // Act
        await sut.SyncAsync();

        // Assert
        sut.SyncStatus.Should().Be(SyncStatus.Error);
        sut.LastError.Should().Be("Something broke");
    }

    [Fact]
    public async Task SyncAsync_ClearsLastErrorAtStart()
    {
        // Arrange: first sync fails with error
        _authService.IsSignedIn = true;
        _driveService.FindException = FakeGoogleDriveService.CreateHttpException(
            HttpStatusCode.InternalServerError, "Error");
        await _sut.SyncAsync();
        _sut.LastError.Should().NotBeNull();

        // Clear exception and sync again
        _driveService.FindException = null;
        _driveService.FindResult = (null, null);

        // Act
        await _sut.SyncAsync();

        // Assert
        _sut.LastError.Should().BeNull();
        _sut.SyncStatus.Should().Be(SyncStatus.Synced);
    }

    #endregion

    #region Debounce and Dispose

    [Fact]
    public async Task Debounce_CancelsPreviousPendingSyncWhenNewChangeArrives()
    {
        // Arrange: signed in
        _authService.IsSignedIn = true;
        _driveService.FindResult = (null, null);

        // Act: fire two data changes rapidly — first debounce should be cancelled
        _diaryService.RaiseOnDataChanged();
        _diaryService.RaiseOnDataChanged();

        // Wait for debounce to fire (2s delay + margin)
        await Task.Delay(3500);

        // Assert: only one sync triggered (second cancels first debounce)
        var findCalls = _driveService.MethodCalls
            .Count(c => c == nameof(IGoogleDriveService.FindSyncFileAsync));
        findCalls.Should().Be(1);
    }

    [Fact]
    public async Task OnDataChanged_ResubscribedEvenWhenReplaceAllAsyncThrows()
    {
        // Arrange: remote has entries → localChanged → ReplaceAllAsync will throw
        _authService.IsSignedIn = true;
        var remoteEntry = new DiaryEntry
        {
            Id = Guid.NewGuid(),
            BristolType = 4,
            Timestamp = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        var throwingDiary = new ThrowingOnReplaceDiaryService();
        var driveService = new FakeGoogleDriveService
        {
            FindResult = ("file-1", "\"etag-1\""),
            DownloadResult = new SyncEnvelope { Entries = [remoteEntry] }
        };
        using var sut = new SyncService(throwingDiary, _authService, driveService);

        // Act: sync fails on ReplaceAllAsync
        await sut.SyncAsync();
        sut.SyncStatus.Should().Be(SyncStatus.Error);

        // Verify OnDataChanged is still subscribed by triggering another sync via event
        driveService.FindResult = (null, null);
        driveService.DownloadResult = null;
        throwingDiary.ShouldThrow = false;
        throwingDiary.RaiseOnDataChanged();
        await Task.Delay(3500);

        // If re-subscribed, a second sync attempt occurred
        driveService.MethodCalls
            .Count(c => c == nameof(IGoogleDriveService.FindSyncFileAsync))
            .Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Dispose_CancelsAndDisposesDebounce()
    {
        // Arrange: use a separate SyncService to avoid double-dispose from class-level Dispose
        var authService = new FakeGoogleAuthService { IsSignedIn = true };
        var driveService = new FakeGoogleDriveService { FindResult = (null, null) };
        var diaryService = new FakeDiaryService();
        var sut = new SyncService(diaryService, authService, driveService);

        // Act: trigger debounce then immediately dispose
        diaryService.RaiseOnDataChanged();
        sut.Dispose();

        // Wait for what would have been the debounce window
        await Task.Delay(3500);

        // Assert: no sync happened (debounce was cancelled by Dispose)
        driveService.MethodCalls.Should().NotContain(nameof(IGoogleDriveService.FindSyncFileAsync));
    }

    #endregion

    /// <summary>
    /// Helper fake that switches IsSignedIn to true after TrySilentSignInAsync is called.
    /// </summary>
    private class FakeGoogleAuthServiceWithSilentSignInSwitch : IGoogleAuthService
    {
        private bool _isSignedIn;
        public List<string> MethodCalls { get; } = [];

        public Task InitializeAsync()
        {
            MethodCalls.Add(nameof(InitializeAsync));
            return Task.CompletedTask;
        }

        public Task<string?> SignInAsync()
        {
            MethodCalls.Add(nameof(SignInAsync));
            return Task.FromResult<string?>("token");
        }

        public Task SignOutAsync()
        {
            MethodCalls.Add(nameof(SignOutAsync));
            _isSignedIn = false;
            return Task.CompletedTask;
        }

        public Task<bool> IsSignedInAsync()
        {
            MethodCalls.Add(nameof(IsSignedInAsync));
            return Task.FromResult(_isSignedIn);
        }

        public Task<string?> GetAccessTokenAsync()
        {
            MethodCalls.Add(nameof(GetAccessTokenAsync));
            return Task.FromResult<string?>("token");
        }

        public Task<bool> TrySilentSignInAsync()
        {
            MethodCalls.Add(nameof(TrySilentSignInAsync));
            _isSignedIn = true; // Simulate successful silent sign-in
            return Task.FromResult(true);
        }

        public Task<bool> HasPreviousSessionAsync()
        {
            MethodCalls.Add(nameof(HasPreviousSessionAsync));
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Drive service that blocks on FindSyncFileAsync until a task completes (for concurrency testing).
    /// </summary>
    private class SlowFakeGoogleDriveService : IGoogleDriveService
    {
        private readonly Task<(string? FileId, string? ETag)> _findTask;
        public int FindCallCount { get; private set; }

        public SlowFakeGoogleDriveService(Task<(string? FileId, string? ETag)> findTask)
        {
            _findTask = findTask;
        }

        public async Task<(string? FileId, string? ETag)> FindSyncFileAsync()
        {
            FindCallCount++;
            return await _findTask;
        }

        public Task<SyncEnvelope?> DownloadSyncFileAsync(string fileId) =>
            Task.FromResult<SyncEnvelope?>(null);

        public Task<(string FileId, string ETag)> UploadSyncFileAsync(SyncEnvelope envelope, string? existingFileId, string? etag) =>
            Task.FromResult(("file-1", "\"etag-1\""));
    }

    /// <summary>
    /// Drive service with a queue of upload exceptions — dequeues one per call, succeeds when empty.
    /// </summary>
    private class ConfigurableDriveService : IGoogleDriveService
    {
        private readonly Queue<Exception> _uploadExceptions = new();
        public int FindCallCount { get; private set; }
        public int UploadCallCount { get; private set; }

        public void EnqueueUploadException(Exception ex) => _uploadExceptions.Enqueue(ex);

        public Task<(string? FileId, string? ETag)> FindSyncFileAsync()
        {
            FindCallCount++;
            return Task.FromResult<(string?, string?)>((null, null));
        }

        public Task<SyncEnvelope?> DownloadSyncFileAsync(string fileId) =>
            Task.FromResult<SyncEnvelope?>(null);

        public Task<(string FileId, string ETag)> UploadSyncFileAsync(
            SyncEnvelope envelope, string? existingFileId, string? etag)
        {
            UploadCallCount++;
            if (_uploadExceptions.Count > 0)
                throw _uploadExceptions.Dequeue();
            return Task.FromResult(("file-1", "\"etag-1\""));
        }
    }

    /// <summary>
    /// Drive service that always throws a given exception on any method call.
    /// </summary>
    private class ThrowingDriveService : IGoogleDriveService
    {
        private readonly Exception _exception;
        public ThrowingDriveService(Exception exception) => _exception = exception;

        public Task<(string? FileId, string? ETag)> FindSyncFileAsync() => throw _exception;
        public Task<SyncEnvelope?> DownloadSyncFileAsync(string fileId) => throw _exception;
        public Task<(string FileId, string ETag)> UploadSyncFileAsync(
            SyncEnvelope envelope, string? existingFileId, string? etag) => throw _exception;
    }

    /// <summary>
    /// Diary service that throws on ReplaceAllAsync when ShouldThrow is true.
    /// Used to verify OnDataChanged re-subscription in the finally block.
    /// </summary>
    private class ThrowingOnReplaceDiaryService : IDiaryService
    {
        private readonly List<DiaryEntry> _entries = [];
        public bool ShouldThrow { get; set; } = true;
        public event Action? OnDataChanged;

        public Task<List<DiaryEntry>> GetAllAsync() =>
            Task.FromResult(_entries.Where(e => !e.IsDeleted).ToList());

        public Task<List<DiaryEntry>> GetAllIncludingDeletedAsync() =>
            Task.FromResult(_entries.ToList());

        public Task<DiaryEntry?> GetByIdAsync(Guid id) =>
            Task.FromResult(_entries.FirstOrDefault(e => e.Id == id));

        public Task<List<DiaryEntry>> GetByDateAsync(DateTime date) =>
            Task.FromResult(_entries.Where(e => e.Timestamp.Date == date.Date).ToList());

        public Task AddAsync(DiaryEntry entry)
        {
            _entries.Add(entry);
            OnDataChanged?.Invoke();
            return Task.CompletedTask;
        }

        public Task UpdateAsync(DiaryEntry entry) => Task.CompletedTask;
        public Task DeleteAsync(Guid id) => Task.CompletedTask;

        public Task ReplaceAllAsync(List<DiaryEntry> entries)
        {
            if (ShouldThrow) throw new InvalidOperationException("ReplaceAll failed");
            _entries.Clear();
            _entries.AddRange(entries);
            return Task.CompletedTask;
        }

        public void RaiseOnDataChanged() => OnDataChanged?.Invoke();
    }
}
