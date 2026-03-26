using System.Net;
using System.Text.Json;
using FluentAssertions;
using TurdTracker.Models;
using TurdTracker.Services;
using TurdTracker.Tests.Fakes;
using Xunit;

namespace TurdTracker.Tests.Services;

public class GoogleDriveServiceTests : IDisposable
{
    private readonly FakeHttpMessageHandler _handler;
    private readonly HttpClient _httpClient;
    private readonly FakeGoogleAuthService _authService;
    private readonly GoogleDriveService _sut;

    public GoogleDriveServiceTests()
    {
        _handler = new FakeHttpMessageHandler();
        _httpClient = new HttpClient(_handler);
        _authService = new FakeGoogleAuthService { AccessToken = "test-token" };
        _sut = new GoogleDriveService(_httpClient, _authService);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    [Fact]
    public async Task FindSyncFileAsync_ReturnsFileIdAndEtag_WhenFileExists()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            files = new[] { new { id = "file-123" } }
        });

        _handler.RespondTo("drive/v3/files?", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        });

        // Second request fetches the individual file to get ETag from response header
        var fileResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":\"file-123\"}", System.Text.Encoding.UTF8, "application/json"),
            Headers = { ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"etag-abc\"") }
        };
        _handler.RespondTo("drive/v3/files/file-123", fileResponse);

        var (fileId, etag) = await _sut.FindSyncFileAsync();

        fileId.Should().Be("file-123");
        etag.Should().Be("\"etag-abc\"");
    }

    [Fact]
    public async Task FindSyncFileAsync_ReturnsNulls_WhenNoFileFound()
    {
        var responseJson = JsonSerializer.Serialize(new { files = Array.Empty<object>() });

        _handler.RespondTo("drive/v3/files?", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        });

        var (fileId, etag) = await _sut.FindSyncFileAsync();

        fileId.Should().BeNull();
        etag.Should().BeNull();
    }

    [Fact]
    public async Task DownloadSyncFileAsync_DeserializesSyncEnvelope()
    {
        var envelope = new SyncEnvelope
        {
            Version = "1",
            LastSyncedUtc = new DateTime(2026, 3, 25, 12, 0, 0, DateTimeKind.Utc),
            Entries =
            [
                new DiaryEntry
                {
                    Id = Guid.NewGuid(),
                    BristolType = 4,
                    Timestamp = DateTime.UtcNow,
                    Notes = "Test entry"
                }
            ]
        };

        var responseJson = JsonSerializer.Serialize(envelope);
        _handler.RespondTo("alt=media", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        });

        var result = await _sut.DownloadSyncFileAsync("file-123");

        result.Should().NotBeNull();
        result!.Version.Should().Be("1");
        result.Entries.Should().HaveCount(1);
        result.Entries[0].BristolType.Should().Be(4);
        result.Entries[0].Notes.Should().Be("Test entry");
    }

    [Fact]
    public async Task UploadSyncFileAsync_CreatesNewFile_WhenFileIdIsNull()
    {
        var responseJson = JsonSerializer.Serialize(new { id = "new-file-id" });

        _handler.RespondTo("uploadType=multipart", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json"),
            Headers = { ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"new-etag\"") }
        });

        var envelope = new SyncEnvelope { Version = "1", Entries = [] };
        var (fileId, etag) = await _sut.UploadSyncFileAsync(envelope, null, null);

        fileId.Should().Be("new-file-id");
        etag.Should().Be("\"new-etag\"");

        var request = _handler.SentRequests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.ToString().Should().Contain("upload/drive/v3/files");
    }

    [Fact]
    public async Task UploadSyncFileAsync_UpdatesExistingFile_WithIfMatchEtagHeader()
    {
        var responseJson = JsonSerializer.Serialize(new { id = "existing-id" });

        _handler.RespondTo("uploadType=media", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json"),
            Headers = { ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"updated-etag\"") }
        });

        var envelope = new SyncEnvelope { Version = "1", Entries = [] };
        var (fileId, etag) = await _sut.UploadSyncFileAsync(envelope, "existing-id", "\"old-etag\"");

        fileId.Should().Be("existing-id");
        etag.Should().Be("\"updated-etag\"");

        var request = _handler.SentRequests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Patch);
        request.Headers.GetValues("If-Match").Should().ContainSingle().Which.Should().Be("\"old-etag\"");
    }

    [Fact]
    public async Task UploadSyncFileAsync_ThrowsHttpRequestException_On412Conflict()
    {
        _handler.RespondTo("uploadType=media", new HttpResponseMessage(HttpStatusCode.PreconditionFailed));

        var envelope = new SyncEnvelope { Version = "1", Entries = [] };

        var act = () => _sut.UploadSyncFileAsync(envelope, "existing-id", "\"stale-etag\"");

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
