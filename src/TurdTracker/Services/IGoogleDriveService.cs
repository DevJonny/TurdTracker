using TurdTracker.Models;

namespace TurdTracker.Services;

public interface IGoogleDriveService
{
    Task<(string? FileId, string? Version)> FindSyncFileAsync();
    Task<SyncEnvelope?> DownloadSyncFileAsync(string fileId);
    Task<(string FileId, string Version)> UploadSyncFileAsync(SyncEnvelope envelope, string? existingFileId, string? version);
}
