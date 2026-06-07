using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using PlikShare.Antiforgery;
using PlikShare.AuditLog;
using PlikShare.Core.CORS;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.PreSignedLinks.Contracts;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Files.PreSignedLinks.Validation;
using PlikShare.Files.Preview.GetZipBulkDownloadLink;
using PlikShare.Files.Records;
using PlikShare.Storages;
using PlikShare.Storages.Encryption.Authorization;
using PlikShare.Storages.Exceptions;
using PlikShare.Storages.Zip;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Cache;
using PlikShare.Uploads.CompleteFileUpload;
using PlikShare.Uploads.FilePartUpload.Complete;
using PlikShare.Uploads.Id;
using PlikShare.Workspaces.Cache;
using Serilog;
using System.Buffers;
using Audit = PlikShare.AuditLog.Details.Audit;

// ReSharper disable PossibleMultipleEnumeration

namespace PlikShare.Files.PreSignedLinks;


public static class PreSignedFilesEndpoints
{
    private const int MaximumFileUploadPayloadSizeInBytes = Aes256GcmStreamingV1.MaximumPayloadSize;

    private const string TotalSizeInBytesHeader = "x-total-size-in-bytes";
    private const string NumberOfFilesHeader = "x-number-of-files";

    public static void MapPreSignedFilesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/files")
            .WithTags("PreSignedFiles")
            .RequireCors(CorsPolicies.PreSignedLink);
        
        group.MapGet("/{protectedPayload}", DownloadFile)
            .WithName("DownloadFile")
            .AddEndpointFilter<ValidateProtectedDownloadPayloadFilter>();

        //antiforgery is disabled for upload endpoints because the links are already
        //pre-signed with endpoint for which antiforgery is on.
        //also S3 pre-signed links does not support antiforgery so these endpoints will stick to that

        group.MapPost("/multi-file/{protectedPayload}", MultiFileDirectUpload)
            .WithName("MultiFileDirectUpload")
            .AddEndpointFilter<ValidateProtectedMultiFileDirectUploadPayloadFilter>()
            .WithMetadata(new DisableAutoAntiforgeryCheck());

        group.MapPut("/{protectedPayload}", UploadFile)
            .WithName("UploadFiles")
            .AddEndpointFilter<ValidateProtectedUploadPayloadFilter>()
            .WithMetadata(new DisableAutoAntiforgeryCheck());
    }

    public static void MapPreSignedZipFilesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/zip-files")
            .WithTags("PreSignedZipFiles")
            .RequireCors(CorsPolicies.PreSignedLink);

        group.MapGet("/{protectedPayload}", DownloadZipFileContent)
            .WithName("DownloadZipFileContent")
            .AddEndpointFilter<ValidateProtectedZipContentDownloadPayloadFilter>();
    }

    public static void MapPreSignedZipBulkDownloadEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/zip-files-bulk")
            .WithTags("PreSignedZipBulkDownload")
            .RequireCors(CorsPolicies.PreSignedLink);

        group.MapGet("/{protectedPayload}", DownloadZipBulkContent)
            .WithName("DownloadZipBulkContent")
            .AddEndpointFilter<ValidateProtectedZipBulkDownloadPayloadFilter>();
    }

    private static async ValueTask<Results<Ok<List<MultiFileDirectUploadItemResponseDto>>, NotFound<HttpError>, BadRequest<HttpError>>> MultiFileDirectUpload(
        HttpContext httpContext,
        InsertFileUploadPartQuery insertFileUploadPartQuery,
        BulkConvertDirectFileUploadsToFilesQuery bulkConvertDirectFileUploadsToFilesQuery,
        FileUploadCache fileUploadCache,
        AuditLogService auditLogService,
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
        
        var heapBuffer = ArrayPool<byte>.Shared.Rent(
            minimumLength: totalSizeInBytes);

        var heapBufferMemory = heapBuffer
            .AsMemory()
            .Slice(0, totalSizeInBytes);
        
        try
        {
            var (fileUploads, badRequest) = await ReadAllFilesIntoBuffer(
                fileUploadCache,
                new MultipartReader(boundary, httpContext.Request.Body),
                heapBufferMemory,
                cancellationToken);

            if (badRequest is not null)
                return badRequest;

            if (fileUploads.Count == 0)
                return HttpErrors.File.NoFilesToUpload();

            var uploadTasks = new List<Task<FileUploadContext?>>();

            var fileOffset = 0;

            foreach (var fileUpload in fileUploads)
            {
                var fileSize = (int) fileUpload.FileToUpload.SizeInBytes;
                
                var uploadTask = ProcessDirectFileUploadAsync(
                    fileBytes: heapBufferMemory.Slice(fileOffset, fileSize),
                    fileUpload: fileUpload,
                    workspace: workspace,
                    workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession(),
                    cancellationToken: cancellationToken);

                uploadTasks.Add(uploadTask);

                fileOffset += fileSize;
            }

            var processedFileUploads = (await Task.WhenAll(uploadTasks))
                .Where(result => result != null)
                .ToList();
            
            var conversionResult = await bulkConvertDirectFileUploadsToFilesQuery.Execute(
                workspace: workspace,
                workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession(),
                fileUploadIds: processedFileUploads.Select(upload => upload!.Id).ToArray(),
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
                    FileExternalId = upload!.FileToUpload.FileKey.FileExternalId,
                    UploadExternalId = upload.ExternalId
                })
                .ToList();

            if (processedFileUploads.Count > 0)
            {
                await auditLogService.Log(
                    Audit.File.MultiUploadCompletedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        fileUploads: processedFileUploads.Select(u => new Audit.FileUploadRef
                        {
                            ExternalId = u!.ExternalId,
                            FileExternalId = u.FileToUpload.FileKey.FileExternalId,
                            Name = u.FileName,
                            Extension = u.FileExtension,
                            SizeInBytes = u.FileToUpload.SizeInBytes,
                            FolderPath = u.FolderAncestors.ToFolderPath()
                        }).ToList()),
                    cancellationToken);
            }

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

            var fileSizeInBytes = (int)fileUpload.FileToUpload.SizeInBytes;
            
            await section.Body.ReadExactlyAsync(
                heapBufferMemory.Slice(offset, fileSizeInBytes),
                cancellationToken);

            offset += fileSizeInBytes;
        }

        return (fileUploads, null);
    }

    private static async Task<FileUploadContext?> ProcessDirectFileUploadAsync(
        Memory<byte> fileBytes,
        FileUploadContext fileUpload,
        WorkspaceContext workspace,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        CancellationToken cancellationToken)
    {
        try
        {
            var encryptionMode = workspace.GetFileEncryptionMode(
                fileEncryptionMetadata: fileUpload.FileToUpload.EncryptionMetadata,
                workspaceEncryptionSession: workspaceEncryptionSession);

            var uploadDetails = new UploadFilePartDetails(
                FileKey: fileUpload.FileToUpload.FileKey,
                MultipartUploadId: fileUpload.FileToUpload.MultipartUploadId,
                FileSizeInBytes: fileUpload.FileToUpload.SizeInBytes,
                Part: FilePart.First((int) fileUpload.FileToUpload.SizeInBytes),
                UploadAlgorithm: UploadAlgorithm.DirectUpload,
                EncryptionMode: encryptionMode);

            var result = await workspace.UploadFilePart(
                input: fileBytes,
                uploadDetails: uploadDetails,
                cancellationToken: cancellationToken);

            Log.Debug(
                "Successfully uploaded file part. FileUploadId: {FileUploadId}, PartNumber: {PartNumber}, Algorithm: {Algorithm}, ETag: {ETag}",
                fileUpload.Id, 1, fileUpload.UploadAlgorithm, result.ETag);

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
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var (payload, fileUpload, workspace) = httpContext.GetProtectedUploadPayload();

        Log.Debug("File part upload with pre-signed url started: {Payload}", payload);

        var partSizeInBytes = (int) httpContext.Request.ContentLength!.Value;

        if (partSizeInBytes > MaximumFileUploadPayloadSizeInBytes)
            return HttpErrors.File.PayloadTooBig(partSizeInBytes);

        try
        {
            var encryptionMode = workspace.GetFileEncryptionMode(
                fileEncryptionMetadata: fileUpload.FileToUpload.EncryptionMetadata,
                workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession());

            var uploadDetails = new UploadFilePartDetails(
                FileKey: fileUpload.FileToUpload.FileKey,
                MultipartUploadId: fileUpload.FileToUpload.MultipartUploadId,
                FileSizeInBytes: fileUpload.FileToUpload.SizeInBytes,
                Part: new FilePart(payload.PartNumber, partSizeInBytes),
                UploadAlgorithm: fileUpload.UploadAlgorithm,
                EncryptionMode: encryptionMode);

            var result = await workspace.UploadFilePart(
                input: httpContext.Request.BodyReader,
                uploadDetails: uploadDetails,
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

    private static async ValueTask<Results<EmptyHttpResult, NotFound<HttpError>, JsonHttpResult<HttpError>>> DownloadFile(
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var protectedDownloadPayload = httpContext.GetProtectedDownloadPayload();

        var (payload, file, workspace) = protectedDownloadPayload;

        Log.Debug("File download with pre-signed url started: {Payload}",
            payload);

        var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

        await auditLogService.Log(
            Audit.File.DownloadedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspace: workspace.ToAuditLogWorkspaceRef(),
                file: new Audit.FileRef
                {
                    ExternalId = payload.FileExternalId,

                    Name = workspace.EncodeMetadata(
                        file.Name,
                        workspaceEncryptionSession),

                    Extension = workspace.EncodeMetadata(
                        file.Extension,
                        workspaceEncryptionSession),

                    SizeInBytes = file.SizeInBytes,

                    FolderPath = file
                        .FolderPath
                        ?.Select(value => workspace.EncodeMetadata(
                            value, 
                            workspaceEncryptionSession))
                        .ToList()
                }),
            cancellationToken);


        var rangeRequest = httpContext.TryGetRangeRequest(
            fileSizeInBytes: file.SizeInBytes);

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
    
    private static async ValueTask<Results<EmptyHttpResult, NotFound<HttpError>, JsonHttpResult<HttpError>>> HandleRangeFileDownload(
        RangeRequest rangeRequest,
        ProtectedDownloadPayload protectedPayload,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (payload, file, workspace) = protectedPayload;

        if (!rangeRequest.IsValid(file.SizeInBytes))
        {
            httpContext.Response.Headers.ContentRange = rangeRequest.InvalidContentRange(file.SizeInBytes);
            
            return HttpErrors.File.RangeNotSatisfiable(
                payload.FileExternalId);
        }

        try
        {
            var encryptionMode = workspace.GetFileEncryptionMode(
                fileEncryptionMetadata: file.EncryptionMetadata,
                workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession());

            await using var storageFileRange = await workspace.DownloadFileRange(
                fileDetails: new DownloadFileRangeDetails(
                    Range: rangeRequest.Range,
                    FileKey: new FileKey
                    {
                        KeySecretPart = file.KeySecretPart,
                        FileExternalId = file.ExternalId,
                    },
                    FileSizeInBytes: file.SizeInBytes,
                    EncryptionMode: encryptionMode),
                cancellationToken: cancellationToken);
            
            httpContext.Response.Headers.AcceptRanges = "bytes";
            httpContext.Response.Headers.ContentType = file.ContentType;
            httpContext.Response.Headers.ContentRange = rangeRequest.ValidContentRange(file.SizeInBytes);
            httpContext.Response.Headers.ContentLength = rangeRequest.Range.Length;
            httpContext.Response.Headers.ContentDisposition = ContentDispositionHelper.CreateContentDisposition(
                fileName: file.FullName,
                disposition: payload.ContentDisposition);
            
            httpContext.Response.StatusCode = StatusCodes.Status206PartialContent;

            await storageFileRange.ReadTo(
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

            if (httpContext.Response.HasStarted)
            {
                httpContext.Abort();
                return TypedResults.Empty;
            }

            return HttpErrors.File.DownloadStreamingFailed(
                payload.FileExternalId);
        }
        finally
        {
            await httpContext.Response.BodyWriter.CompleteAsync();
        }
    }

    private static async ValueTask<Results<EmptyHttpResult, NotFound<HttpError>, JsonHttpResult<HttpError>>> HandleFullFileDownload(
        ProtectedDownloadPayload protectedPayload,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (payload, file, workspace) = protectedPayload;


        try
        {
            var encryptionMode = workspace.GetFileEncryptionMode(
                fileEncryptionMetadata: file.EncryptionMetadata,
                workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession());

            await using var storageFile = await workspace.DownloadFile(
                fileDetails: new DownloadFileDetails(
                    FileKey: new FileKey
                    {
                        KeySecretPart = file.KeySecretPart,
                        FileExternalId = file.ExternalId,
                    },
                    FileSizeInBytes: file.SizeInBytes,
                    EncryptionMode: encryptionMode),
                cancellationToken: cancellationToken);
            
            httpContext.Response.Headers.AcceptRanges = "bytes";
            httpContext.Response.Headers.ContentType = file.ContentType;
            httpContext.Response.Headers.ContentDisposition = ContentDispositionHelper.CreateContentDisposition(
                fileName: file.FullName,
                disposition: payload.ContentDisposition);

            httpContext.Response.Headers.ContentLength = file.SizeInBytes;

            await storageFile.ReadTo(
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

            if (httpContext.Response.HasStarted)
            {
                httpContext.Abort();
                return TypedResults.Empty;
            }

            return HttpErrors.File.DownloadStreamingFailed(
                payload.FileExternalId);
        }
        finally
        {
            if (httpContext.Response.HasStarted)
            {
                await httpContext.Response.BodyWriter.CompleteAsync();
            }
        }
    }

    private static async ValueTask<Results<EmptyHttpResult, NotFound<HttpError>, JsonHttpResult<HttpError>>> DownloadZipFileContent(
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
            var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

            await ZipEntryReader.ReadEntryAsync(
                file: file,
                entry: payload.ZipEntry,
                workspace: workspace,
                output: httpContext.Response.BodyWriter,
                getFileEncryptionMode: f => workspace.GetFileEncryptionMode(
                    fileEncryptionMetadata: f.EncryptionMetadata,
                    workspaceEncryptionSession: workspaceEncryptionSession),
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

            if (httpContext.Response.HasStarted)
            {
                httpContext.Abort();
                return TypedResults.Empty;
            }

            return HttpErrors.File.DownloadStreamingFailed(
                payload.FileExternalId);
        }
        finally
        {
            await httpContext.Response.BodyWriter.CompleteAsync();
        }
    }

    private static async ValueTask<Results<EmptyHttpResult, NotFound<HttpError>, JsonHttpResult<HttpError>>> DownloadZipBulkContent(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var protectedPayload = httpContext.GetProtectedZipBulkDownloadPayload();
        var (payload, file, workspace) = protectedPayload;

        Log.Debug("Zip bulk download with pre-signed url started: {Payload}", payload);

        try
        {
            var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();
            
            // CDFH must be read before any response bytes go out — both because the
            // decoded entries drive the selection plan and because a broken zip has
            // to surface as an error while we can still write headers.
            var decodingResult = await ZipDecoder.ReadZipEntries(
                file: file,
                workspace: workspace,
                getFileEncryptionMode: f => workspace.GetFileEncryptionMode(
                    fileEncryptionMetadata: f.EncryptionMetadata,
                    workspaceEncryptionSession: workspaceEncryptionSession),
                cancellationToken: cancellationToken);

            if (decodingResult.Code == ZipDecoder.ZipDecodingResultCode.ZipFileBroken)
            {
                Log.Warning("Zip bulk download: source zip '{FileExternalId}' is broken.",
                    payload.FileExternalId);

                return HttpErrors.File.NotFound(payload.FileExternalId);
            }

            if (decodingResult.Code != ZipDecoder.ZipDecodingResultCode.Ok)
                throw new UnexpectedOperationResultException(
                    operationName: nameof(ZipDecoder),
                    resultValueStr: decodingResult.Code.ToString());

            var outputFileName = $"{file.Name}-selection.zip";

            httpContext.Response.Headers.ContentType = "application/zip";
            httpContext.Response.Headers.ContentDisposition = ContentDispositionHelper.CreateContentDisposition(
                fileName: outputFileName,
                disposition: ContentDispositionType.Attachment);

            await ZipBulkDownloadStreamer.StreamAsync(
                sourceFile: file,
                workspace: workspace,
                payload: payload,
                cdfhEntries: decodingResult.Entries!,
                output: httpContext.Response.BodyWriter,
                getFileEncryptionMode: f => workspace.GetFileEncryptionMode(
                    fileEncryptionMetadata: f.EncryptionMetadata,
                    workspaceEncryptionSession: workspaceEncryptionSession),
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
                "Could not execute zip bulk download with pre-signed url because file '{FileExternalId}' was not found.",
                payload.FileExternalId);

            return HttpErrors.File.NotFound(payload.FileExternalId);
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while zip-bulk-downloading from file '{FileExternalId}' with pre-signed url.",
                payload.FileExternalId);

            if (httpContext.Response.HasStarted)
            {
                httpContext.Abort();
                return TypedResults.Empty;
            }

            return HttpErrors.File.DownloadStreamingFailed(
                payload.FileExternalId);
        }
        finally
        {
            if (httpContext.Response.HasStarted)
            {
                await httpContext.Response.BodyWriter.CompleteAsync();
            }
        }
    }
}



public record FilePartUploadResult(
    string ETag);

