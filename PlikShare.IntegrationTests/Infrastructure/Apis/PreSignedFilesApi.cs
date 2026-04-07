using Flurl.Http;
using PlikShare.Files.PreSignedLinks.Contracts;
using PlikShare.Uploads.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class PreSignedFilesApi(IFlurlClient flurlClient)
{
    public async Task<List<MultiFileDirectUploadItemResponseDto>> MultiFileDirectUpload(
        string preSignedUrl,
        Dictionary<FileUploadExtId, byte[]> files,
        SessionAuthCookie? cookie)
    {
        var totalSize = files.Values.Sum(f => f.Length);

        var request = flurlClient
            .Request(preSignedUrl)
            .AllowAnyHttpStatus()
            .WithCookie(cookie);

        request.Headers.Add("x-total-size-in-bytes", totalSize.ToString());
        request.Headers.Add("x-number-of-files", files.Count.ToString());

        var response = await request
            .PostMultipartAsync(mp =>
            {
                foreach (var (uploadExternalId, content) in files)
                {
                    mp.AddFile(
                        name: "file",
                        stream: new MemoryStream(content),
                        fileName: uploadExternalId.Value);
                }
            });

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            throw new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);
        }

        return await response.GetJsonAsync<List<MultiFileDirectUploadItemResponseDto>>();
    }

    public async Task<string> UploadFilePart(
        string preSignedUrl,
        byte[] content,
        string contentType,
        SessionAuthCookie? cookie)
    {
        var request = flurlClient
            .Request(preSignedUrl)
            .AllowAnyHttpStatus()
            .WithCookie(cookie);

        var byteContent = new ByteArrayContent(content);
        byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        byteContent.Headers.ContentLength = content.Length;

        var response = await request.SendAsync(HttpMethod.Put, byteContent);

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            throw new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);
        }

        var eTag = response.ResponseMessage.Headers.ETag?.Tag ?? string.Empty;
        return eTag;
    }

    public async Task<byte[]> DownloadFile(
        string preSignedUrl,
        SessionAuthCookie? cookie)
    {
        var response = await flurlClient
            .Request(preSignedUrl)
            .AllowAnyHttpStatus()
            .WithCookie(cookie)
            .GetAsync();

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            throw new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);
        }

        return await response.GetBytesAsync();
    }

    public async Task<RangeDownloadResult> DownloadFileRange(
        string preSignedUrl,
        long rangeStart,
        long rangeEnd,
        SessionAuthCookie? cookie)
    {
        var response = await flurlClient
            .Request(preSignedUrl)
            .AllowAnyHttpStatus()
            .WithCookie(cookie)
            .WithHeader("Range", $"bytes={rangeStart}-{rangeEnd}")
            .GetAsync();

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            throw new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);
        }

        return new RangeDownloadResult(
            Content: await response.GetBytesAsync(),
            StatusCode: response.StatusCode,
            ContentRange: response.ResponseMessage.Content.Headers.ContentRange?.ToString());
    }

    public record RangeDownloadResult(
        byte[] Content,
        int StatusCode,
        string? ContentRange);
}
