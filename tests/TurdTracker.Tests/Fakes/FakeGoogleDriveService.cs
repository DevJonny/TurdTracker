using System.Net;
using TurdTracker.Models;
using TurdTracker.Services;

namespace TurdTracker.Tests.Fakes;

public class FakeGoogleDriveService : IGoogleDriveService
{
    public (string? FileId, string? ETag) FindResult { get; set; } = (null, null);
    public SyncEnvelope? DownloadResult { get; set; }
    public (string FileId, string ETag) UploadResult { get; set; } = ("file-1", "\"etag-1\"");

    /// <summary>When set, the corresponding method throws this exception.</summary>
    public HttpRequestException? FindException { get; set; }
    public HttpRequestException? DownloadException { get; set; }
    public HttpRequestException? UploadException { get; set; }

    public List<string> MethodCalls { get; } = [];
    public SyncEnvelope? LastUploadedEnvelope { get; private set; }
    public string? LastUploadFileId { get; private set; }
    public string? LastUploadEtag { get; private set; }

    public Task<(string? FileId, string? ETag)> FindSyncFileAsync()
    {
        MethodCalls.Add(nameof(FindSyncFileAsync));
        if (FindException != null) throw FindException;
        return Task.FromResult(FindResult);
    }

    public Task<SyncEnvelope?> DownloadSyncFileAsync(string fileId)
    {
        MethodCalls.Add(nameof(DownloadSyncFileAsync));
        if (DownloadException != null) throw DownloadException;
        return Task.FromResult(DownloadResult);
    }

    public Task<(string FileId, string ETag)> UploadSyncFileAsync(SyncEnvelope envelope, string? existingFileId, string? etag)
    {
        MethodCalls.Add(nameof(UploadSyncFileAsync));
        LastUploadedEnvelope = envelope;
        LastUploadFileId = existingFileId;
        LastUploadEtag = etag;
        if (UploadException != null) throw UploadException;
        return Task.FromResult(UploadResult);
    }

    /// <summary>Helper to create HttpRequestException with a status code.</summary>
    public static HttpRequestException CreateHttpException(HttpStatusCode statusCode, string message = "")
    {
        return new HttpRequestException(message, null, statusCode);
    }
}
