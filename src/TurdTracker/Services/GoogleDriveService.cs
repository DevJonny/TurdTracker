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

    public async Task<(string? FileId, string? Version)> FindSyncFileAsync()
    {
        await SetAuthHeaderAsync();

        var url = $"{DriveApiBase}/files?spaces=appDataFolder&q=name%3D'{SyncFileName}'&fields=files(id,version)";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FileListResponse>();
        var file = result?.Files?.FirstOrDefault();

        return file != null ? (file.Id, file.Version) : (null, null);
    }

    public async Task<SyncEnvelope?> DownloadSyncFileAsync(string fileId)
    {
        await SetAuthHeaderAsync();

        var url = $"{DriveApiBase}/files/{fileId}?alt=media";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SyncEnvelope>();
    }

    public async Task<(string FileId, string Version)> UploadSyncFileAsync(SyncEnvelope envelope, string? existingFileId, string? version)
    {
        await SetAuthHeaderAsync();

        var jsonContent = JsonSerializer.Serialize(envelope);

        if (existingFileId != null)
        {
            return await UpdateFileAsync(existingFileId, version, jsonContent);
        }
        else
        {
            return await CreateFileAsync(jsonContent);
        }
    }

    private async Task<(string FileId, string Version)> CreateFileAsync(string jsonContent)
    {
        var metadata = new { name = SyncFileName, parents = new[] { "appDataFolder" } };
        var metadataJson = JsonSerializer.Serialize(metadata);

        var multipartContent = new MultipartContent("related");
        var metadataPart = new StringContent(metadataJson, System.Text.Encoding.UTF8, "application/json");
        var filePart = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

        multipartContent.Add(metadataPart);
        multipartContent.Add(filePart);

        var url = $"{DriveUploadBase}/files?uploadType=multipart&fields=id,version";
        var response = await _httpClient.PostAsync(url, multipartContent);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FileResponse>();
        return (result!.Id!, result.Version!);
    }

    private async Task<(string FileId, string Version)> UpdateFileAsync(string fileId, string? version, string jsonContent)
    {
        var url = $"{DriveUploadBase}/files/{fileId}?uploadType=media&fields=id,version";
        var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
        };

        if (version != null)
        {
            request.Headers.Add("If-Match", version);
        }

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FileResponse>();
        return (result!.Id!, result.Version!);
    }

    private async Task SetAuthHeaderAsync()
    {
        var token = await _authService.GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
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

        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }
}
