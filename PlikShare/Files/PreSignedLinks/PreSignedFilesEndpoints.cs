using System.Buffers;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using PlikShare.Core.Authorization;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.PreSignedLinks.Contracts;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Files.PreSignedLinks.Validation;
using PlikShare.Files.Records;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Exceptions;
using PlikShare.Storages.FileReading;
using PlikShare.Storages.HardDrive.StorageClient;
using PlikShare.Storages.HardDrive.Upload;
using PlikShare.Storages.S3;
using PlikShare.Storages.S3.Upload;
using PlikShare.Storages.Zip;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Cache;
using PlikShare.Uploads.CompleteFileUpload;
using PlikShare.Uploads.FilePartUpload.Complete;
using PlikShare.Uploads.Id;
using Serilog;

// ReSharper disable PossibleMultipleEnumeration

namespace PlikShare.Files.PreSignedLinks;


public static class PreSignedFilesEndpoints
{
    private const int MaximumFileUploadPayloadSizeInBytes = Aes256GcmStreaming.MaximumPayloadSize;

    private const string TotalSizeInBytesHeader = "x-total-size-in-bytes";
    private const string NumberOfFilesHeader = "x-number-of-files";

    public static void MapPreSignedFilesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/files")
            .WithTags("PreSignedFiles")
            .RequireAuthorization(policyNames: AuthPolicy.InternalOrBoxLink);

        group.MapPost("/multi-file/{protectedPayload}", MultiFileDirectUpload)
            .WithName("MultiFileDirectUpload")
            .AddEndpointFilter<ValidateProtectedMultiFileDirectUploadPayloadFilter>();

        group.MapPut("/{protectedPayload}", UploadFile)
            .WithName("UploadFiles")
            .AddEndpointFilter<ValidateProtectedUploadPayloadFilter>();

        group.MapGet("/{protectedPayload}", DownloadFile)
            .WithName("DownloadFile")
            .AddEndpointFilter<ValidateProtectedDownloadPayloadFilter>();
    }

    public static void MapPreSignedZipFilesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/zip-files")
            .WithTags("PreSignedZipFiles")
            .RequireAuthorization(policyNames: AuthPolicy.InternalOrBoxLink);

        group.MapGet("/{protectedPayload}", DownloadZipFileContent)
            .WithName("DownloadZipFileContent")
            .AddEndpointFilter<ValidateProtectedZipContentDownloadPayloadFilter>();
    }

    private static async ValueTask<Results<Ok<List<MultiFileDirectUploadItemResponseDto>>, NotFound<HttpError>, BadRequest<HttpError>>> MultiFileDirectUpload(
        HttpContext httpContext,
        InsertFileUploadPartQuery insertFileUploadPartQuery,
        BulkConvertDirectFileUploadsToFilesQuery bulkConvertDirectFileUploadsToFilesQuery,
        FileUploadCache fileUploadCache,
        CancellationToken cancellationToken)
    {
        var (payload, workspace) = httpContext.GetProtectedMultiFileDirectUploadPayload();

        Log.Debug("Multi-File direct upload with pre-signed url started: {Payload}", payload);

        if (!httpContext.Request.HasFormContentType)
            return HttpErrors.File.ExpectedMultipartFormDataContent();

        var boundary = httpContext
            .Request
            .GetMultipartBoundary();

        if (string.IsNullOrEmpty(boundary))
            return HttpErrors.File.MissingContentTypeBoundary();

        if (!int.TryParse(httpContext.Request.Headers[TotalSizeInBytesHeader], out var totalSizeInBytes))
            return HttpErrors.File.MissingRequestHeader(TotalSizeInBytesHeader);

        if (totalSizeInBytes > MaximumFileUploadPayloadSizeInBytes)
            return HttpErrors.File.PayloadTooBig(totalSizeInBytes);

        if (!int.TryParse(httpContext.Request.Headers[NumberOfFilesHeader], out var numberOfFiles))
            return HttpErrors.File.MissingRequestHeader(NumberOfFilesHeader);

        var heapBufferSize = workspace.Storage.EncryptionType == StorageEncryptionType.None
            ? totalSizeInBytes
            : Aes256GcmStreaming.CalculateSafeBufferSizeForMultiFileUploads(totalSizeInBytes, numberOfFiles);

        var heapBuffer = ArrayPool<byte>.Shared.Rent(
            minimumLength: heapBufferSize);

        var heapBufferMemory = heapBuffer
            .AsMemory()
            .Slice(0, heapBufferSize);
        
        try
        {
            var (fileUploads, badRequest) = await ReadAllFilesIntoBuffer(
                fileUploadCache,
                new MultipartReader(boundary, httpContext.Request.Body),
                heapBufferMemory,
                workspace.Storage.EncryptionType,
                cancellationToken);

            if (badRequest is not null)
                return badRequest;

            if (fileUploads.Count == 0)
                return HttpErrors.File.NoFilesToUpload();

            var uploadTasks = new List<Task<FileUploadContext?>>();

            var fileOffset = 0;
            foreach (var fileUpload in fileUploads)
            {
                var fileBufferSize = workspace.Storage.EncryptionType == StorageEncryptionType.None
                    ? (int) fileUpload.FileToUpload.SizeInBytes
                    : Aes256GcmStreaming.CalculateEncryptedPartSize((int) fileUpload.FileToUpload.SizeInBytes, 1);
                
                var uploadTask = ProcessDirectFileUploadAsync(
                    heapBufferMemory.Slice(fileOffset, fileBufferSize),
                    fileUpload,
                    workspace.Storage,
                    cancellationToken);

                uploadTasks.Add(uploadTask);

                fileOffset += fileBufferSize;
            }

            var processedFileUploads = (await Task.WhenAll(uploadTasks))
                .Where(result => result != null)
                .ToList();
            
            var conversionResult = await bulkConvertDirectFileUploadsToFilesQuery.Execute(
                fileUploadIds: processedFileUploads.Select(upload => upload!.Id).ToArray(),
                workspaceId: workspace.Id,
                correlationId: httpContext.GetCorrelationId(),
                cancellationToken: cancellationToken);

            if (conversionResult == BulkConvertDirectFileUploadsToFilesQuery.ResultCode.Ok)
            {
                foreach (var processedFileUpload in processedFileUploads)
                {
                    await fileUploadCache.Invalidate(
                        uploadExternalId: processedFileUpload!.ExternalId,
                        cancellationToken: cancellationToken);
                }
            }

            var results = processedFileUploads
                .Select(upload => new MultiFileDirectUploadItemResponseDto
                {
                    FileExternalId = upload!.FileToUpload.S3FileKey.FileExternalId,
                    UploadExternalId = upload.ExternalId
                })
                .ToList();

            return TypedResults.Ok(results);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(
                array: heapBuffer);
        }
    }

    private static async Task<(List<FileUploadContext> fileUploads, BadRequest<HttpError>? badRequest)> ReadAllFilesIntoBuffer(
        FileUploadCache fileUploadCache, 
        MultipartReader reader,
        Memory<byte> heapBufferMemory, 
        StorageEncryptionType encryptionType,
        CancellationToken cancellationToken)
    {
        var fileUploads = new List<FileUploadContext>();
        var offset = 0;

        while (await reader.ReadNextSectionAsync(cancellationToken) is { } section)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition))
                continue;

            var fileName = contentDisposition.FileName.Value;

            if (string.IsNullOrEmpty(fileName))
                return (fileUploads, HttpErrors.File.MissingFileName());

            var uploadExternalId = FileUploadExtId.Parse(
                fileName);

            var fileUpload = await fileUploadCache.GetFileUpload(
                uploadExternalId,
                cancellationToken);

            if (fileUpload == null)
            {
                Log.Warning("FileUpload not found for ExternalId: '{UploadExternalId}'", uploadExternalId);
                continue;
            }

            fileUploads.Add(fileUpload);

            if (encryptionType == StorageEncryptionType.None)
            {
                await section.Body.ReadExactlyAsync(
                    heapBufferMemory.Slice(offset, (int) fileUpload.FileToUpload.SizeInBytes),
                    cancellationToken);

                offset += (int) fileUpload.FileToUpload.SizeInBytes;
            }
            else
            {
                var encryptedPartSize = Aes256GcmStreaming.CalculateEncryptedPartSize(
                    partSizeInBytes: (int) fileUpload.FileToUpload.SizeInBytes,
                    partNumber: 1);

                await section.Body.CopyIntoBufferReadyForInPlaceEncryption(
                    output: heapBufferMemory.Slice(offset, encryptedPartSize),
                    partSizeInBytes: (int) fileUpload.FileToUpload.SizeInBytes,
                    partNumber: 1);

                offset += encryptedPartSize;
            }
        }

        return (fileUploads, null);
    }

    private static async Task<FileUploadContext?> ProcessDirectFileUploadAsync(
        Memory<byte> fileBytes,
        FileUploadContext fileUpload,
        IStorageClient storageClient,
        CancellationToken cancellationToken)
    {
        try
        {
            var filePart = FilePartDetails.First(
                sizeInBytes: (int)fileUpload.FileToUpload.SizeInBytes,
                uploadAlgorithm: UploadAlgorithm.DirectUpload);

            if (storageClient is HardDriveStorageClient hardDriveStorageClient)
            {
                var result = await HardDriveUploadOperation.Execute(
                    fileBytes: fileBytes,
                    file: fileUpload.FileToUpload,
                    part: filePart,
                    bucketName: fileUpload.Workspace.BucketName,
                    hardDriveStorage: hardDriveStorageClient!,
                    cancellationToken: cancellationToken);
            }
            else if (storageClient is S3StorageClient s3StorageClient)
            {
                var result = await S3UploadOperation.Execute(
                    fileBytes: fileBytes,
                    file: fileUpload.FileToUpload,
                    part: filePart,
                    bucketName: fileUpload.Workspace.BucketName,
                    s3StorageClient: s3StorageClient,
                    cancellationToken: cancellationToken);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(storageClient));
            }

            Log.Debug(
                "Successfully uploaded file part. FileUploadId: {FileUploadId}, PartNumber: {PartNumber}, Algorithm: {Algorithm}",
                fileUpload.Id, 1, fileUpload.UploadAlgorithm);

            return fileUpload;
        }
        catch (Exception ex) when (ex is not NotSupportedException)
        {
            Log.Error(ex, "Error processing file upload for '{UploadExternalId}'", fileUpload.ExternalId);
            return null;
        }
    }

    private static async ValueTask<Results<Ok, Ok<FileExtId>, NotFound<HttpError>, BadRequest<HttpError>>> UploadFile(
        HttpContext httpContext,
        InsertFileUploadPartQuery insertFileUploadPartQuery,
        FileUploadCache fileUploadCache,
        CancellationToken cancellationToken)
    {
        var (payload, fileUpload) = httpContext.GetProtectedUploadPayload();

        Log.Debug("File part upload with pre-signed url started: {Payload}", payload);

        var partSizeInBytes = (int) httpContext.Request.ContentLength!.Value;

        if (partSizeInBytes > MaximumFileUploadPayloadSizeInBytes)
            return HttpErrors.File.PayloadTooBig(partSizeInBytes);

        try
        {
            var result = await FileWriter.Write(
                file: fileUpload.FileToUpload,
                part: new FilePartDetails(
                    Number: payload.PartNumber,
                    SizeInBytes: partSizeInBytes,
                    UploadAlgorithm: fileUpload.UploadAlgorithm),
                workspace: fileUpload.Workspace,
                input: httpContext.Request.BodyReader,
                cancellationToken: cancellationToken);

            Log.Debug(
                "Successfully uploaded file part. FileUploadId: {FileUploadId}, PartNumber: {PartNumber}, Algorithm: {Algorithm}",
                fileUpload.Id, payload.PartNumber, fileUpload.UploadAlgorithm);

            if (fileUpload.UploadAlgorithm == UploadAlgorithm.MultiStepChunkUpload)
            {
                var insertPartResult = await insertFileUploadPartQuery.Execute(
                    fileUploadId: fileUpload.Id,
                    partNumber: payload.PartNumber,
                    eTag: result.ETag,
                    cancellationToken: cancellationToken);

                if (insertPartResult.Code == InsertFileUploadPartQuery.ResultCode.FileUploadNotFound)
                {
                    Log.Warning(
                        "Could not finalize file part upload with pre-signed url because FileUpload '{FileUploadExternalId}' was not found.",
                        payload.FileUploadExternalId);

                    return HttpErrors.Upload.NotFound(
                        payload.FileUploadExternalId);
                }
            }
            else
            {
                Log.Error(
                    "Unsupported upload algorithm encountered. Algorithm: {Algorithm}, FileUploadId: {FileUploadId}",
                    fileUpload.UploadAlgorithm, fileUpload.Id);

                throw new NotSupportedException($"Upload algorithm {fileUpload.UploadAlgorithm} is not supported");
            }

            return TypedResults.Ok();
        }
        catch (Exception ex) when (ex is not NotSupportedException)
        {
            Log.Error(ex, "Unexpected error during file upload. FileUploadId: {FileUploadId}, PartNumber: {PartNumber}",
                fileUpload.Id, payload.PartNumber);

            throw;
        }
    }

    private static async ValueTask<Results<EmptyHttpResult, NotFound<HttpError>, StatusCodeHttpResult>> DownloadFile(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var protectedDownloadPayload = httpContext.GetProtectedDownloadPayload();

        var (payload, file, _) = protectedDownloadPayload;

        Log.Debug("File download with pre-signed url started: {Payload}",
            payload);

        var rangeRequest = httpContext.TryGetRangeRequest(
            fileSizeInBytes: file.SizeInBytes);

        httpContext.Response.Headers.AcceptRanges = "bytes";
        httpContext.Response.Headers.ContentType = file.ContentType;
        httpContext.Response.Headers.ContentDisposition = ContentDispositionHelper.CreateContentDisposition(
            fileName: file.FullName,
            disposition: payload.ContentDisposition);

        if (rangeRequest.IsRangeRequest)
        {
            Log.Debug("Range request handling started: {Range}", rangeRequest.Range);

            return await HandleRangeFileDownload(
                rangeRequest: rangeRequest,
                httpContext: httpContext,
                protectedPayload: protectedDownloadPayload,
                cancellationToken: cancellationToken);
        }

        return await HandleFullFileDownload(
            protectedPayload: protectedDownloadPayload,
            httpContext: httpContext,
            cancellationToken: cancellationToken);
    }
    
    private static async ValueTask<Results<EmptyHttpResult, NotFound<HttpError>, StatusCodeHttpResult>> HandleRangeFileDownload(
        RangeRequest rangeRequest,
        ProtectedDownloadPayload protectedPayload,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (payload, file, workspace) = protectedPayload;

        if (!rangeRequest.IsValid(file.SizeInBytes))
        {
            httpContext.Response.Headers.ContentRange = rangeRequest.InvalidContentRange(file.SizeInBytes);
            return TypedResults.StatusCode(StatusCodes.Status416RangeNotSatisfiable);
        }

        httpContext.Response.Headers.ContentRange = rangeRequest.ValidContentRange(file.SizeInBytes);
        httpContext.Response.Headers.ContentLength = rangeRequest.Range.Length;
        httpContext.Response.StatusCode = StatusCodes.Status206PartialContent;

        try
        {
            await FileReader.ReadRange(
                s3FileKey: new S3FileKey
                {
                    S3KeySecretPart = file.S3KeySecretPart,
                    FileExternalId = file.ExternalId,
                },
                fileEncryption: file.Encryption,
                fileSizeInBytes: file.SizeInBytes,
                range: rangeRequest.Range,
                workspace: workspace,
                output: httpContext.Response.BodyWriter,
                cancellationToken: cancellationToken);

            return TypedResults.Empty;
        }
        catch (OperationCanceledException)
        {
            //if the streaming is cancelled in the middle we just let it end and do nothing
            return TypedResults.Empty;
        }
        catch (FileNotFoundInStorageException e)
        {
            Log.Warning(e,
                "Could not execute file download with pre-signed url because file '{FileExternalId}' was not found.",
                payload.FileExternalId);

            return HttpErrors.File.NotFound(
                payload.FileExternalId);
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while downloading a file '{FileExternalId}' with pre-signed url.",
                payload.FileExternalId);

            throw;
        }
        finally
        {
            await httpContext.Response.BodyWriter.CompleteAsync();
        }
    }

    private static async ValueTask<Results<EmptyHttpResult, NotFound<HttpError>, StatusCodeHttpResult>> HandleFullFileDownload(
        ProtectedDownloadPayload protectedPayload,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (payload, file, workspace) = protectedPayload;

        httpContext.Response.Headers.ContentLength = file.SizeInBytes;

        try
        {
            await FileReader.ReadFull(
                s3FileKey: new S3FileKey
                {
                    S3KeySecretPart = file.S3KeySecretPart,
                    FileExternalId = file.ExternalId,
                },
                fileSizeInBytes: file.SizeInBytes,
                workspace: workspace,
                output: httpContext.Response.BodyWriter,
                cancellationToken: cancellationToken);

            return TypedResults.Empty;
        }
        catch (OperationCanceledException)
        {
            //if the streaming is cancelled in the middle we just let it end and do nothing
            return TypedResults.Empty;
        }
        catch (FileNotFoundInStorageException e)
        {
            Log.Warning(e, "Could not execute file download with pre-signed url because file '{FileExternalId}' was not found.",
                payload.FileExternalId);
            
            return HttpErrors.File.NotFound(
                payload.FileExternalId);
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while downloading a file '{FileExternalId}' with pre-signed url.",
                payload.FileExternalId);

            throw;
        }
        finally
        {
            await httpContext.Response.BodyWriter.CompleteAsync();
        }
    }

    private static async ValueTask<Results<EmptyHttpResult, NotFound<HttpError>>> DownloadZipFileContent(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var protectedDownloadPayload = httpContext.GetProtectedZipContentDownloadPayload();

        var (payload, file, workspace) = protectedDownloadPayload;

        Log.Debug("File download with pre-signed url started: {Payload}",
            payload);
        
        httpContext.Response.Headers.AcceptRanges = "bytes";
        httpContext.Response.Headers.ContentType = ContentTypeHelper.GetContentType(payload.ZipEntry.FileName);
        httpContext.Response.Headers.ContentDisposition = ContentDispositionHelper.CreateContentDisposition(
            fileName: payload.ZipEntry.FileName,
            disposition: payload.ContentDisposition);
        
        httpContext.Response.Headers.ContentLength = payload.ZipEntry.SizeInBytes;

        try
        {
            await ZipEntryReader.ReadEntryAsync(
                file: file,
                entry: payload.ZipEntry,
                workspace: workspace,
                output: httpContext.Response.BodyWriter,
                cancellationToken: cancellationToken);

            return TypedResults.Empty;
        }
        catch (OperationCanceledException)
        {
            //if the streaming is cancelled in the middle we just let it end and do nothing
            return TypedResults.Empty;
        }
        catch (FileNotFoundInStorageException e)
        {
            Log.Warning(e,
                "Could not execute zip content download with pre-signed url because file '{FileExternalId}' was not found.",
                payload.FileExternalId);

            return HttpErrors.File.NotFound(
                payload.FileExternalId);
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while downloading a file '{FileExternalId}' with pre-signed url.",
                payload.FileExternalId);

            throw;
        }
        finally
        {
            await httpContext.Response.BodyWriter.CompleteAsync();
        }
    }
}



public readonly record struct FilePartUploadResult(
    string ETag);

