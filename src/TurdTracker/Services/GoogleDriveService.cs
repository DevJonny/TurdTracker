using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TurdTracker.Models;

namespace TurdTracker.Services;

public class GoogleDriveService : IGoogleDriveService
{
    private const string SyncFileName = "turdtracker-sync.json";
    private const string DriveApiBase = "https://www.googleapis.com/drive/v3";
    private const string DriveUploadBase = "https://www.googleapis.com/upload/drive/v3";

    private readonly HttpClient _httpClient;
    private readonly IGoogleAuthService _authService;

    public GoogleDriveService(HttpClient httpClient, IGoogleAuthService authService)
    {
        _httpClient = httpClient;
        _authService = authService;
    }

    public async Task<(string? FileId, string? ETag)> FindSyncFileAsync()
    {
        var token = await GetAuthTokenAsync();

        var url = $"{DriveApiBase}/files?spaces=appDataFolder&q=name%3D'{SyncFileName}'&fields=files(id,etag)";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FileListResponse>();
        var file = result?.Files?.FirstOrDefault();

        return file != null ? (file.Id, file.ETag) : (null, null);
    }

    public async Task<SyncEnvelope?> DownloadSyncFileAsync(string fileId)
    {
        var token = await GetAuthTokenAsync();

        var url = $"{DriveApiBase}/files/{fileId}?alt=media";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SyncEnvelope>();
    }

    public async Task<(string FileId, string ETag)> UploadSyncFileAsync(SyncEnvelope envelope, string? existingFileId, string? etag)
    {
        var jsonContent = JsonSerializer.Serialize(envelope);

        if (existingFileId != null)
        {
            return await UpdateFileAsync(existingFileId, etag, jsonContent);
        }
        else
        {
            return await CreateFileAsync(jsonContent);
        }
    }

    private async Task<(string FileId, string ETag)> CreateFileAsync(string jsonContent)
    {
        var token = await GetAuthTokenAsync();

        var metadata = new { name = SyncFileName, parents = new[] { "appDataFolder" } };
        var metadataJson = JsonSerializer.Serialize(metadata);

        var multipartContent = new MultipartContent("related");
        var metadataPart = new StringContent(metadataJson, System.Text.Encoding.UTF8, "application/json");
        var filePart = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

        multipartContent.Add(metadataPart);
        multipartContent.Add(filePart);

        var url = $"{DriveUploadBase}/files?uploadType=multipart&fields=id,etag";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = multipartContent
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FileResponse>();
        return (result!.Id!, result.ETag!);
    }

    private async Task<(string FileId, string ETag)> UpdateFileAsync(string fileId, string? etag, string jsonContent)
    {
        var token = await GetAuthTokenAsync();

        var url = $"{DriveUploadBase}/files/{fileId}?uploadType=media&fields=id,etag";
        var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (etag != null)
        {
            request.Headers.Add("If-Match", etag);
        }

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FileResponse>();
        return (result!.Id!, result.ETag!);
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var token = await _authService.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("Google access token is null or empty. User may not be signed in.");
        }
        return token;
    }

    private class FileListResponse
    {
        [JsonPropertyName("files")]
        public List<FileResponse>? Files { get; set; }
    }

    private class FileResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("etag")]
        public string? ETag { get; set; }
    }
}
