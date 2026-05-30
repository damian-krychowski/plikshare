using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using PlikShare.AuditLog;
using PlikShare.Core.Authorization;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Download.Contracts;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Files.Records;
using PlikShare.MediaProcessing.Generation;
using PlikShare.MediaProcessing.Generation.Contracts;
using PlikShare.Storages;
using PlikShare.Storages.Encryption.Authorization;
using PlikShare.Storages.Exceptions;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Validation;
using System.Globalization;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.MediaProcessing;

public static class MediaProcessingEndpoints
{
    private const int MaximumFileUploadPayloadSizeInBytes = Aes256GcmStreamingV1.MaximumPayloadSize;

    public static void MapMediaProcessingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceExternalId}/media")
            .WithTags("MediaProcessing")
            .RequireAuthorization(policyNames: AuthPolicy.Internal)
            .AddEndpointFilter<ValidateWorkspaceFilter>()
            .AddEndpointFilter<ValidateWorkspaceEncryptionSessionFilter>();

        group.MapPost("/thumbnails/{fileExternalId}", UploadFileThumbnail)
            .WithName("UploadFileThumbnail");

        group.MapDelete("/thumbnails/{fileExternalId}/{variant}", DeleteFileThumbnail)
            .WithName("DeleteFileThumbnail");

        group.MapPost("/thumbnails/{fileExternalId}/generate", GenerateFileThumbnails)
            .WithName("GenerateFileThumbnails");

        group.MapPost("/thumbnails/generate-bulk", GenerateFileThumbnailsBulk)
            .WithName("GenerateFileThumbnailsBulk");

        group.MapGet("/thumbnails/batches/{batchId:guid}/status", GetThumbnailGenerationStatus)
            .WithName("GetThumbnailGenerationStatus");

        group.MapGet("/thumbnails/batches/{batchId:guid}/events", GetThumbnailBatchEvents)
            .WithName("GetThumbnailBatchEvents");

        group.MapGet("/thumbnails/{fileExternalId}", GetFileThumbnail)
            .WithName("GetFileThumbnail");

        group.MapGet("/{fileExternalId}/convert", DownloadFileConverted)
            .WithName("DownloadFileConverted");
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UploadFileThumbnail(
        [FromRoute] FileExtId fileExternalId,
        HttpContext httpContext,
        UploadFileThumbnailOperation uploadFileThumbnailOperation,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        if (!httpContext.Request.HasFormContentType)
            return HttpErrors.File.ExpectedMultipartFormDataContent();

        var form = await httpContext.Request.ReadFormAsync(cancellationToken);

        if (!form.Files.Any())
            return HttpErrors.File.MissingFile();

        var file = form.Files[0];
        var fileName = FileNames.TryGetNameAndExtension(file.FileName);

        if (fileName is null)
            return HttpErrors.File.MissingFileName();

        if (file.Length > MaximumFileUploadPayloadSizeInBytes)
            return HttpErrors.File.PayloadTooBig(file.Length);

        if (ContentTypeHelper.GetFileTypeFromExtension(fileName.Extension) != FileType.Image)
            return HttpErrors.File.ThumbnailMustBeImage();

        if (!form.TryGetValue("fileExternalId", out var externalIdValues)
            || string.IsNullOrEmpty(externalIdValues)
            || externalIdValues.Count != 1
            || !FileExtId.TryParse(
                externalIdValues[0],
                CultureInfo.InvariantCulture,
                out var thumbnailFileExternalId))
        {
            return HttpErrors.File.MissingAttachmentFileExternalId();
        }

        if (!form.TryGetValue("variant", out var variantValues)
            || string.IsNullOrEmpty(variantValues)
            || variantValues.Count != 1
            || !Enum.TryParse<ThumbnailVariant>(
                variantValues[0],
                ignoreCase: true,
                out var variant))
        {
            return HttpErrors.File.InvalidThumbnailVariant();
        }

        var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

        await using var fileStream = file.OpenReadStream();

        var result = await uploadFileThumbnailOperation.Execute(
            workspace: workspaceMembership.Workspace,
            parentFileExternalId: fileExternalId,
            thumbnailFileExternalId: thumbnailFileExternalId,
            variant: variant,
            thumbnailContent: fileStream,
            thumbnailSizeInBytes: file.Length,
            thumbnailContentType: file.ContentType,
            thumbnailFileName: fileName.Name,
            thumbnailFileExtension: fileName.Extension,
            uploader: new UserIdentity(
                UserExternalId: workspaceMembership.User.ExternalId),
            workspaceEncryptionSession: workspaceEncryptionSession,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        if (result.Code == UploadFileThumbnailOperation.ResultCode.ParentNotFound)
            return HttpErrors.File.NotFound(fileExternalId);

        if (result.Code == UploadFileThumbnailOperation.ResultCode.ParentNotThumbnailable)
            return HttpErrors.File.ParentNotThumbnailable();

        await auditLogService.LogWithFileContext(
            fileExternalId: fileExternalId,
            buildEntry: fileRef => Audit.File.AttachmentUploadedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                parentFile: fileRef,
                attachment: new Audit.FileRef
                {
                    ExternalId = result.Attachment!.ExternalId,
                    Name = workspaceEncryptionSession.Encode(fileName.Name),
                    Extension = workspaceEncryptionSession.Encode(fileName.Extension),
                    SizeInBytes = result.Attachment.SizeInBytes
                }),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> DeleteFileThumbnail(
        [FromRoute] FileExtId fileExternalId,
        [FromRoute] string variant,
        HttpContext httpContext,
        DeleteFileThumbnailOperation deleteFileThumbnailOperation,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ThumbnailVariant>(
                variant,
                ignoreCase: true,
                out var parsedVariant))
        {
            return HttpErrors.File.InvalidThumbnailVariant();
        }

        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();
        var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

        var result = await deleteFileThumbnailOperation.Execute(
            workspace: workspaceMembership.Workspace,
            parentFileExternalId: fileExternalId,
            variant: parsedVariant,
            workspaceEncryptionSession: workspaceEncryptionSession,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        if (result.Code == DeleteFileThumbnailOperation.ResultCode.ParentNotFound)
            return HttpErrors.File.NotFound(fileExternalId);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok<GenerateFileThumbnailsResponseDto>, NotFound<HttpError>, BadRequest<HttpError>, JsonHttpResult<HttpError>>> GenerateFileThumbnails(
        [FromRoute] FileExtId fileExternalId,
        [FromBody] GenerateFileThumbnailsRequestDto request,
        HttpContext httpContext,
        GenerateFileThumbnailsOperation generateFileThumbnailsOperation,
        CancellationToken cancellationToken)
    {
        if (request.Variants is null || request.Variants.Count == 0)
            return HttpErrors.File.NoThumbnailVariantsRequested();

        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();
        var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

        var result = await generateFileThumbnailsOperation.Execute(
            workspace: workspaceMembership.Workspace,
            parentFileExternalId: fileExternalId,
            variants: request.Variants,
            triggeredByUserExternalId: workspaceMembership.User.ExternalId,
            workspaceEncryptionSession: workspaceEncryptionSession,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GenerateFileThumbnailsOperation.ResultCode.Ok => TypedResults.Ok(new GenerateFileThumbnailsResponseDto
            {
                BatchId = result.BatchId!.Value
            }),
            GenerateFileThumbnailsOperation.ResultCode.FfmpegUnavailable => HttpErrors.File.FfmpegUnavailable(),
            GenerateFileThumbnailsOperation.ResultCode.ParentNotFound => HttpErrors.File.NotFound(fileExternalId),
            GenerateFileThumbnailsOperation.ResultCode.ParentNotThumbnailable => HttpErrors.File.ParentNotThumbnailable(),
            GenerateFileThumbnailsOperation.ResultCode.NoVariants => HttpErrors.File.NoThumbnailVariantsRequested(),
            _ => HttpErrors.File.NotFound(fileExternalId)
        };
    }

    private static async Task<Results<Ok<GenerateFileThumbnailsBulkResponseDto>, BadRequest<HttpError>, JsonHttpResult<HttpError>>> GenerateFileThumbnailsBulk(
        [FromBody] GenerateFileThumbnailsBulkRequestDto request,
        HttpContext httpContext,
        GenerateFileThumbnailsBulkOperation generateFileThumbnailsBulkOperation,
        CancellationToken cancellationToken)
    {
        if (request.Variants is null || request.Variants.Count == 0)
            return HttpErrors.File.NoThumbnailVariantsRequested();

        if (request.FileExternalIds is null || request.FileExternalIds.Count == 0)
            return HttpErrors.File.NoThumbnailableFilesSelected();

        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();
        var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

        var result = await generateFileThumbnailsBulkOperation.Execute(
            workspace: workspaceMembership.Workspace,
            parentFileExternalIds: request.FileExternalIds,
            variants: request.Variants,
            triggeredByUserExternalId: workspaceMembership.User.ExternalId,
            workspaceEncryptionSession: workspaceEncryptionSession,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GenerateFileThumbnailsBulkOperation.ResultCode.Ok => TypedResults.Ok(new GenerateFileThumbnailsBulkResponseDto
            {
                BatchId = result.BatchId!.Value,
                TotalFiles = result.TotalFiles
            }),
            GenerateFileThumbnailsBulkOperation.ResultCode.FfmpegUnavailable => HttpErrors.File.FfmpegUnavailable(),
            GenerateFileThumbnailsBulkOperation.ResultCode.NoVariants => HttpErrors.File.NoThumbnailVariantsRequested(),
            GenerateFileThumbnailsBulkOperation.ResultCode.NoThumbnailableFiles => HttpErrors.File.NoThumbnailableFilesSelected(),
            _ => HttpErrors.File.NoThumbnailableFilesSelected()
        };
    }

    private static Ok<ThumbnailGenerationStatusResponseDto> GetThumbnailGenerationStatus(
        [FromRoute] Guid batchId,
        HttpContext httpContext,
        GetThumbnailGenerationStatusQuery getThumbnailGenerationStatusQuery)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var response = getThumbnailGenerationStatusQuery.Execute(
            workspace: workspaceMembership.Workspace,
            batchId: batchId);

        return TypedResults.Ok(response);
    }

    /// <summary>
    /// Server-Sent Events stream of a thumbnail batch's status. Pushes an initial snapshot, then a
    /// fresh status on every queue notification for the batch, and closes once no variant is still
    /// generating. Replaces client-side polling — the connection is opened once and the server
    /// pushes. Single-process app, so no backplane is needed.
    /// </summary>
    private static async Task GetThumbnailBatchEvents(
        [FromRoute] Guid batchId,
        HttpContext httpContext,
        GetThumbnailGenerationStatusQuery getThumbnailGenerationStatusQuery,
        QueueBatchNotifier queueBatchNotifier,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();
        var workspace = workspaceMembership.Workspace;

        var response = httpContext.Response;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Append("X-Accel-Buffering", "no");

        // Subscribe BEFORE the snapshot so a completion landing between the two isn't missed.
        using var subscription = queueBatchNotifier.Subscribe(
            batchId);

        // Per-connection running state: each signal reads only jobs completed at/after this cursor
        // (so a 1000-job batch isn't re-read every time), deduped by qc_id since the cursor is
        // inclusive. Per-variant errors accumulate across signals; ReadyThumbnails in each push is
        // therefore already a delta.
        DateTimeOffset? lastCompletedAt = null;
        var sentQcIds = new HashSet<long>();
        var failedByVariant = new Dictionary<ThumbnailVariant, string?>();

        ThumbnailGenerationStatusResponseDto NextStatus()
        {
            var snapshot = getThumbnailGenerationStatusQuery.GetSnapshot(
                workspace,
                batchId,
                lastCompletedAt);

            var fresh = snapshot.NewCompleted
                .Where(job => sentQcIds.Add(job.QcId))
                .ToList();

            var ready = new List<ReadyThumbnailDto>();

            GetThumbnailGenerationStatusQuery.Apply(
                fresh,
                failedByVariant,
                ready);

            if (snapshot.NewCompleted.Count > 0)
                lastCompletedAt = snapshot.NewCompleted.Max(job => job.CompletedAt);

            return GetThumbnailGenerationStatusQuery.BuildStatus(
                snapshot.Counts,
                snapshot.GeneratingVariants,
                failedByVariant,
                ready);
        }

        var status = NextStatus();

        await WriteSseStatus(
            response,
            status,
            cancellationToken);

        // Batch is done once nothing is outstanding (covers bulk: all files finished/failed).
        if (status.Pending == 0)
            return;

        var keepAlive = TimeSpan.FromSeconds(20);

        while (!cancellationToken.IsCancellationRequested)
        {
            subscription.DrainPending();

            bool signalled;

            using (var keepAliveCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken))
            {
                keepAliveCts.CancelAfter(keepAlive);

                try
                {
                    signalled = await subscription.WaitForSignalAsync(
                        keepAliveCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Keep-alive tick — comment line keeps the connection (and proxies) alive.
                    await response.WriteAsync(": keep-alive\n\n", cancellationToken);
                    await response.Body.FlushAsync(cancellationToken);
                    continue;
                }
            }

            if (!signalled)
                break;

            status = NextStatus();

            await WriteSseStatus(
                response,
                status,
                cancellationToken);

            if (status.Pending == 0)
                break;
        }
    }

    private static async Task WriteSseStatus(
        HttpResponse response,
        ThumbnailGenerationStatusResponseDto status,
        CancellationToken cancellationToken)
    {
        await response.WriteAsync($"data: {Json.Serialize(status)}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Streams a file's Mini thumbnail (decrypted) for use as an &lt;img src&gt; in the file list.
    /// Cookie-authenticated like the rest of the workspace API, so a plain &lt;img&gt; works.
    /// 404 when the file has no Mini thumbnail. ETag is the thumbnail content hash, so an
    /// identical re-upload keeps the cache and a changed one busts it.
    /// </summary>
    private static async Task<IResult> GetFileThumbnail(
        [FromRoute] FileExtId fileExternalId,
        HttpContext httpContext,
        GetThumbnailDownloadDetailsQuery getThumbnailDownloadDetailsQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();
        var workspace = workspaceMembership.Workspace;
        var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

        var thumbnail = getThumbnailDownloadDetailsQuery.Execute(
            workspace: workspace,
            parentFileExternalId: fileExternalId,
            variant: ThumbnailVariant.Mini,
            workspaceEncryptionSession: workspaceEncryptionSession);

        if (thumbnail is null)
            return HttpErrors.File.NotFound(fileExternalId);

        var file = thumbnail.File;
        var response = httpContext.Response;

        var etag = $"\"{thumbnail.Etag}\"";
        response.Headers.CacheControl = "private, max-age=300";
        response.Headers.ETag = etag;

        if (string.Equals(
                httpContext.Request.Headers.IfNoneMatch.ToString(),
                etag,
                StringComparison.Ordinal))
        {
            response.StatusCode = StatusCodes.Status304NotModified;
            return Results.Empty;
        }

        try
        {
            var encryptionMode = file.EncryptionMetadata.ToEncryptionMode(
                workspaceEncryptionSession: workspaceEncryptionSession,
                storageClient: workspace.Storage);

            await using var storageFile = await workspace.DownloadFile(
                fileDetails: new DownloadFileDetails(
                    FileKey: file.FileKey,
                    FileSizeInBytes: file.SizeInBytes,
                    EncryptionMode: encryptionMode),
                cancellationToken: cancellationToken);

            response.Headers.ContentType = file.ContentType;
            response.Headers.ContentLength = file.SizeInBytes;

            await storageFile.ReadTo(
                output: response.BodyWriter,
                cancellationToken: cancellationToken);

            return Results.Empty;
        }
        catch (OperationCanceledException)
        {
            return Results.Empty;
        }
        catch (FileNotFoundInStorageException)
        {
            if (response.HasStarted)
            {
                httpContext.Abort();
                return Results.Empty;
            }

            return HttpErrors.File.NotFound(fileExternalId);
        }
        finally
        {
            if (response.HasStarted)
                await response.BodyWriter.CompleteAsync();
        }
    }

    private static async Task<IResult> DownloadFileConverted(
        [FromRoute] FileExtId fileExternalId,
        [FromQuery] string format,
        HttpContext httpContext,
        DownloadFileConvertedOperation downloadFileConvertedOperation,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DownloadImageFormat>(
                format,
                ignoreCase: true,
                out var targetFormat))
        {
            return HttpErrors.File.InvalidDownloadFormat();
        }

        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();
        var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

        var response = httpContext.Response;

        var result = await downloadFileConvertedOperation.Execute(
            workspace: workspaceMembership.Workspace,
            parentFileExternalId: fileExternalId,
            targetFormat: targetFormat,
            workspaceEncryptionSession: workspaceEncryptionSession,
            output: response.BodyWriter,
            onMetadataResolved: metadata =>
            {
                // Set headers right before the body streams. From here the response is committed,
                // so the error branches below only ever fire when nothing has been written.
                response.ContentType = metadata.ContentType;

                var contentDisposition = new ContentDispositionHeaderValue("attachment");
                contentDisposition.SetHttpFileName(metadata.DownloadFileName);
                response.Headers.ContentDisposition = contentDisposition.ToString();
            },
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            // Body already streamed to the response — nothing left to write.
            DownloadFileConvertedOperation.ResultCode.Ok => Results.Empty,
            DownloadFileConvertedOperation.ResultCode.FfmpegUnavailable => HttpErrors.File.FfmpegUnavailable(),
            DownloadFileConvertedOperation.ResultCode.ParentNotFound => HttpErrors.File.NotFound(fileExternalId),
            DownloadFileConvertedOperation.ResultCode.ParentNotThumbnailable => HttpErrors.File.ParentNotThumbnailable(),
            _ => HttpErrors.File.NotFound(fileExternalId)
        };
    }
}
