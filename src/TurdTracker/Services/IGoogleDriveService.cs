using TurdTracker.Models;

namespace TurdTracker.Services;

public interface IGoogleDriveService
{
    Task<(string? FileId, string? ETag)> FindSyncFileAsync();
    Task<SyncEnvelope?> DownloadSyncFileAsync(string fileId);
    Task<(string FileId, string ETag)> UploadSyncFileAsync(SyncEnvelope envelope, string? existingFileId, string? etag);
}
