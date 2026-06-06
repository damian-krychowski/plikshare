using Flurl.Http;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Folders.Id;
using PlikShare.MediaProcessing.Generation.Contracts;
using PlikShare.Workspaces.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

/// <summary>
/// Workspace-scoped thumbnail surface: manual upload/delete + queue-driven generate/status +
/// download for &lt;img src&gt;. All under /api/workspaces/{w}/media/thumbnails/... — preview
/// details (which lists the thumbnails attached to a file) still lives on <see cref="FilesApi"/>
/// since that endpoint sits in the files group.
/// </summary>
public class MediaProcessingApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<FileExtId> UploadThumbnail(
        WorkspaceExtId workspaceExternalId,
        FileExtId parentFileExternalId,
        byte[] thumbnailContent,
        string thumbnailFileName,
        string thumbnailContentType,
        ThumbnailVariant variant,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery,
        Cookie? workspaceEncryptionSession = null)
    {
        var thumbnailExternalId = FileExtId.NewId();

        using var formData = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(thumbnailContent);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(thumbnailContentType);

        formData.Add(fileContent, "file", thumbnailFileName);
        formData.Add(new StringContent(thumbnailExternalId.Value), "fileExternalId");
        formData.Add(new StringContent(variant.ToString()), "variant");

        var response = await flurlClient
            .Request(appUrl, $"api/workspaces/{workspaceExternalId}/media/thumbnails/{parentFileExternalId}")
            .AllowAnyHttpStatus()
            .WithCookies(cookie, workspaceEncryptionSession)
            .WithAntiforgery(antiforgery)
            .PostAsync(formData);

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            throw new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode,
                url: response.ResponseMessage.RequestMessage!.RequestUri!.AbsoluteUri);
        }

        return thumbnailExternalId;
    }

    public async Task DeleteThumbnail(
        WorkspaceExtId workspaceExternalId,
        FileExtId parentFileExternalId,
        ThumbnailVariant variant,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery,
        Cookie? workspaceEncryptionSession = null)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/media/thumbnails/{parentFileExternalId}/{variant}",
            cookie: cookie,
            antiforgery: antiforgery,
            extraCookie: workspaceEncryptionSession);
    }

    /// <summary>
    /// Enqueues a thumbnail-generation job for one parent file. Returns the batch id needed for
    /// polling status. Pair with <see cref="WaitForBatchDone"/> to await completion.
    /// </summary>
    public async Task<Guid> GenerateFileThumbnails(
        WorkspaceExtId workspaceExternalId,
        FileExtId fileExternalId,
        List<ThumbnailVariant> variants,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery,
        Cookie? workspaceEncryptionSession = null)
    {
        var response = await flurlClient.ExecutePost<GenerateFileThumbnailsResponseDto, GenerateFileThumbnailsRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/media/thumbnails/{fileExternalId}/generate",
            request: new GenerateFileThumbnailsRequestDto
            {
                Variants = variants
            },
            cookie: cookie,
            antiforgery: antiforgery,
            extraCookie: workspaceEncryptionSession);

        return response.BatchId;
    }

    /// <summary>
    /// Enqueues thumbnail generation for many files under a single batchId. Request body is
    /// Protobuf (the production endpoint expects it — JSON would 415). Returns
    /// (batchId, totalFiles); totalFiles reflects only files that were actually enqueued (i.e.
    /// thumbnailable + still present), matching the production semantics.
    /// </summary>
    public async Task<(Guid BatchId, int TotalFiles)> GenerateFileThumbnailsBulk(
        WorkspaceExtId workspaceExternalId,
        List<ThumbnailVariant> variants,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery,
        List<FileExtId>? selectedFiles = null,
        List<FolderExtId>? selectedFolders = null,
        List<FileExtId>? excludedFiles = null,
        List<FolderExtId>? excludedFolders = null,
        Cookie? workspaceEncryptionSession = null)
    {
        var response = await flurlClient.ExecutePost<GenerateFileThumbnailsBulkResponseDto, GenerateFileThumbnailsBulkRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/media/thumbnails/generate-bulk",
            request: new GenerateFileThumbnailsBulkRequestDto
            {
                SelectedFiles = (selectedFiles ?? []).Select(x => x.Value).ToList(),
                SelectedFolders = (selectedFolders ?? []).Select(x => x.Value).ToList(),
                ExcludedFiles = (excludedFiles ?? []).Select(x => x.Value).ToList(),
                ExcludedFolders = (excludedFolders ?? []).Select(x => x.Value).ToList(),
                Variants = variants.Select(v => v.ToString()).ToList()
            },
            cookie: cookie,
            antiforgery: antiforgery,
            isRequestInProtobuf: true,
            isResponseInProtobuf: true,
            extraCookie: workspaceEncryptionSession);

        return (Guid.Parse(response.BatchId), response.TotalFiles);
    }

    /// <summary>
    /// Resolves the same include/exclude tree selection as generate-bulk and returns how many
    /// thumbnailable files (and their total size) would be processed — without enqueuing anything.
    /// Protobuf both ways, like generate-bulk.
    /// </summary>
    public Task<CountThumbnailableFilesResponseDto> CountThumbnailableFiles(
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery,
        List<FileExtId>? selectedFiles = null,
        List<FolderExtId>? selectedFolders = null,
        List<FileExtId>? excludedFiles = null,
        List<FolderExtId>? excludedFolders = null,
        Cookie? workspaceEncryptionSession = null)
    {
        return flurlClient.ExecutePost<CountThumbnailableFilesResponseDto, CountThumbnailableFilesRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/media/thumbnails/generate-bulk/count",
            request: new CountThumbnailableFilesRequestDto
            {
                SelectedFiles = (selectedFiles ?? []).Select(x => x.Value).ToList(),
                SelectedFolders = (selectedFolders ?? []).Select(x => x.Value).ToList(),
                ExcludedFiles = (excludedFiles ?? []).Select(x => x.Value).ToList(),
                ExcludedFolders = (excludedFolders ?? []).Select(x => x.Value).ToList()
            },
            cookie: cookie,
            antiforgery: antiforgery,
            isRequestInProtobuf: true,
            isResponseInProtobuf: true,
            extraCookie: workspaceEncryptionSession);
    }

    public Task<ThumbnailGenerationStatusResponseDto> GetThumbnailGenerationStatus(
        WorkspaceExtId workspaceExternalId,
        Guid batchId,
        SessionAuthCookie? cookie,
        Cookie? workspaceEncryptionSession = null)
    {
        return flurlClient.ExecuteGet<ThumbnailGenerationStatusResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/media/thumbnails/batches/{batchId}/status",
            cookie: cookie,
            extraCookie: workspaceEncryptionSession);
    }

    /// <summary>
    /// Polls <see cref="GetThumbnailGenerationStatus"/> every <paramref name="pollIntervalMs"/>
    /// until <c>Pending == 0</c> or <paramref name="timeoutMs"/> elapses. Returns the terminal
    /// status. The queue runner is async — without this, follow-up assertions race the worker.
    /// </summary>
    public async Task<ThumbnailGenerationStatusResponseDto> WaitForBatchDone(
        WorkspaceExtId workspaceExternalId,
        Guid batchId,
        SessionAuthCookie? cookie,
        Cookie? workspaceEncryptionSession = null,
        int timeoutMs = 30_000,
        int pollIntervalMs = 100)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (true)
        {
            var status = await GetThumbnailGenerationStatus(
                workspaceExternalId: workspaceExternalId,
                batchId: batchId,
                cookie: cookie,
                workspaceEncryptionSession: workspaceEncryptionSession);

            if (status.Pending == 0)
                return status;

            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException(
                    $"Thumbnail batch {batchId} did not complete within {timeoutMs} ms — " +
                    $"last status: Pending={status.Pending}, Completed={status.Completed}, Failed={status.Failed}");

            await Task.Delay(pollIntervalMs);
        }
    }

    /// <summary>
    /// Downloads the Mini thumbnail bytes for a parent file via the workspace endpoint. Returns
    /// (statusCode, bytes); bytes is empty on non-200.
    /// </summary>
    public async Task<(int StatusCode, byte[] Body)> GetFileThumbnail(
        WorkspaceExtId workspaceExternalId,
        FileExtId fileExternalId,
        SessionAuthCookie? cookie,
        Cookie? workspaceEncryptionSession = null)
    {
        var response = await flurlClient
            .Request(appUrl, $"api/workspaces/{workspaceExternalId}/media/thumbnails/{fileExternalId}")
            .AllowAnyHttpStatus()
            .WithCookies(cookie, workspaceEncryptionSession)
            .GetAsync();

        var body = response.ResponseMessage.IsSuccessStatusCode
            ? await response.GetBytesAsync()
            : [];

        return (response.StatusCode, body);
    }
}
