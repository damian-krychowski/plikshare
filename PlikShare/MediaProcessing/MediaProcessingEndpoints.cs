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
using PlikShare.Core.Protobuf;
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
using PlikShare.Core.Clock;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.MediaProcessing;

public static class MediaProcessingEndpoints
{
    private const int MaximumFileUploadPayloadSizeInBytes = Aes256GcmStreamingV1.MaximumPayloadSize;

    // q_queue ROWS per outstanding chunk in the initial SSE stream. Each row holds up to BatchSize
    // files, so ~200 rows ≈ 2000 file ids per event — keeps the first push small for huge batches.
    private const int OutstandingChunkRowLimit = 200;

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
            .WithName("GenerateFileThumbnailsBulk")
            .WithProtobufResponse();

        group.MapPost("/thumbnails/generate-bulk/count", CountThumbnailableFiles)
            .WithName("CountThumbnailableFiles")
            .WithProtobufResponse();

        group.MapGet("/thumbnails/batches/{batchId:guid}/status", GetThumbnailGenerationStatus)
            .WithName("GetThumbnailGenerationStatus");

        group.MapPost("/thumbnails/batches/{batchId:guid}/cancel", CancelThumbnailBatch)
            .WithName("CancelThumbnailBatch");

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
        var workspace = workspaceMembership.Workspace;

        if (!httpContext.Request.HasFormContentType)
            return HttpErrors.File.ExpectedMultipartFormDataContent();

        var form = await httpContext.Request.ReadFormAsync(
            cancellationToken);

        if (!form.Files.Any())
            return HttpErrors.File.MissingFile();

        var file = form.Files[0];
        var fileName = FileNames.TryGetNameAndExtension(
            file.FileName);

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
        
        var result = await uploadFileThumbnailOperation.Execute(
            workspace: workspace,
            parentFileExternalId: fileExternalId,
            thumbnail: new ThumbnailDescriptor(
                FileKey: new FileKey
                {
                    FileExternalId = thumbnailFileExternalId,
                    KeySecretPart = workspace.GenerateFileKeySecretPart()
                },
                Variant: variant,
                SizeInBytes: file.Length,
                ContentType: file.ContentType,
                FileName: fileName.Name,
                FileExtension: fileName.Extension),
            getContent: () => file.OpenReadStream(),
            uploader: new UserIdentity(
                UserExternalId: workspaceMembership.User.ExternalId),
            workspaceEncryptionSession: workspaceEncryptionSession,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);
        
        if (result.Code == UploadFileThumbnailOperation.ResultCode.ParentNotThumbnailable)
            return HttpErrors.File.ParentNotThumbnailable();

        if (result.Code == UploadFileThumbnailOperation.ResultCode.ParentNotFound)
            return HttpErrors.File.NotFound(fileExternalId);

        await auditLogService.LogWithFileContext(
            fileExternalId: fileExternalId,
            buildEntry: fileRef => Audit.File.AttachmentUploadedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspace: workspace.ToAuditLogWorkspaceRef(),
                parentFile: fileRef,
                attachment: new Audit.FileRef
                {
                    ExternalId = result.Attachment!.ExternalId,
                    SizeInBytes = result.Attachment.SizeInBytes,
                    
                    Name = workspace.EncodeMetadata(
                        fileName.Name,
                        workspaceEncryptionSession),

                    Extension = workspace.EncodeMetadata(
                        fileName.Extension,
                        workspaceEncryptionSession)
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
        GenerateFileThumbnailsBulkOperation generateFileThumbnailsBulkOperation,
        GetThumbnailableSelectionFilesQuery getThumbnailableSelectionFilesQuery,
        CancellationToken cancellationToken)
    {
        if (request.Variants is null || request.Variants.Count == 0)
            return HttpErrors.File.NoThumbnailVariantsRequested();

        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();
        var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

        var thumbnailableFiles = getThumbnailableSelectionFilesQuery
            .Execute(
                workspace: workspaceMembership.Workspace,
                selectedFolders: [],
                selectedFiles: [fileExternalId.Value],
                excludedFolders: [],
                excludedFiles: [],
                workspaceEncryptionSession: workspaceEncryptionSession)
            .Select(file => new GenerateFileThumbnailsBulkOperation.SourceFile
            {
                ExternalId = file.ExternalId,
                Extension = file.Extension,
                EncryptionMetadata = file.EncryptionMetadata
            })
            .ToList();

        var result = await generateFileThumbnailsBulkOperation.Execute(
            workspace: workspaceMembership.Workspace,
            thumbnailableFiles: thumbnailableFiles,
            variants: request.Variants,
            triggeredByUserExternalId: workspaceMembership.User.ExternalId,
            workspaceEncryptionSession: workspaceEncryptionSession,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        if (result.TotalFiles == 0)
        {
            return HttpErrors.File.ParentNotThumbnailable();
        }

        return result.Code switch
        {
            GenerateFileThumbnailsBulkOperation.ResultCode.Ok => TypedResults.Ok(new GenerateFileThumbnailsResponseDto
            {
                BatchId = result.BatchId!.Value
            }),
            GenerateFileThumbnailsBulkOperation.ResultCode.FfmpegUnavailable => HttpErrors.File.FfmpegUnavailable(),
            GenerateFileThumbnailsBulkOperation.ResultCode.NoVariants => HttpErrors.File.NoThumbnailVariantsRequested(),
            _ => HttpErrors.File.NotFound(fileExternalId)
        };
    }

    private static async Task<Results<Ok<GenerateFileThumbnailsBulkResponseDto>, BadRequest<HttpError>, JsonHttpResult<HttpError>>> GenerateFileThumbnailsBulk(
        HttpContext httpContext,
        GenerateFileThumbnailsBulkOperation generateFileThumbnailsBulkOperation,
        GetThumbnailableSelectionFilesQuery getThumbnailableSelectionFilesQuery,
        CancellationToken cancellationToken)
    {
        // Proto body — far cheaper than JSON when the selection spans large folder subtrees.
        var request = httpContext.GetProtobufRequest<GenerateFileThumbnailsBulkRequestDto>();

        if (request.Variants is null || request.Variants.Count == 0)
            return HttpErrors.File.NoThumbnailVariantsRequested();

        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();
        var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

        // Resolve the include/exclude tree selection into the thumbnailable source files
        // (selected files + recursive folder descendants − excluded subtrees − excluded files).
        var thumbnailableFiles = getThumbnailableSelectionFilesQuery.Execute(
            workspace: workspaceMembership.Workspace,
            selectedFolders: request.SelectedFolders ?? [],
            selectedFiles: request.SelectedFiles ?? [],
            excludedFolders: request.ExcludedFolders ?? [],
            excludedFiles: request.ExcludedFiles ?? [],
            workspaceEncryptionSession: workspaceEncryptionSession);

        if (thumbnailableFiles.Count == 0)
            return HttpErrors.File.NoThumbnailableFilesSelected();

        var variants = new List<ThumbnailVariant>(request.Variants.Count);
        foreach (var raw in request.Variants)
        {
            if (!Enum.TryParse<ThumbnailVariant>(raw, ignoreCase: true, out var parsed))
                return HttpErrors.File.InvalidThumbnailVariant();

            variants.Add(parsed);
        }

        var sourceFiles = thumbnailableFiles
            .Select(file => new GenerateFileThumbnailsBulkOperation.SourceFile
            {
                ExternalId = file.ExternalId,
                Extension = file.Extension,
                EncryptionMetadata = file.EncryptionMetadata
            })
            .ToList();

        var result = await generateFileThumbnailsBulkOperation.Execute(
            workspace: workspaceMembership.Workspace,
            thumbnailableFiles: sourceFiles,
            variants: variants,
            triggeredByUserExternalId: workspaceMembership.User.ExternalId,
            workspaceEncryptionSession: workspaceEncryptionSession,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GenerateFileThumbnailsBulkOperation.ResultCode.Ok => TypedResults.Ok(new GenerateFileThumbnailsBulkResponseDto
            {
                BatchId = result.BatchId!.Value.ToString(),
                TotalFiles = result.TotalFiles
            }),
            GenerateFileThumbnailsBulkOperation.ResultCode.FfmpegUnavailable => HttpErrors.File.FfmpegUnavailable(),
            GenerateFileThumbnailsBulkOperation.ResultCode.NoVariants => HttpErrors.File.NoThumbnailVariantsRequested(),
            GenerateFileThumbnailsBulkOperation.ResultCode.NoThumbnailableFiles => HttpErrors.File.NoThumbnailableFilesSelected(),
            _ => HttpErrors.File.NoThumbnailableFilesSelected()
        };
    }

    private static Ok<CountThumbnailableFilesResponseDto> CountThumbnailableFiles(
        HttpContext httpContext,
        GetThumbnailableSelectionFilesQuery getThumbnailableSelectionFilesQuery)
    {
        var request = httpContext.GetProtobufRequest<CountThumbnailableFilesRequestDto>();

        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();
        var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

        var countResult = getThumbnailableSelectionFilesQuery.ExecuteCount(
            workspace: workspaceMembership.Workspace,
            selectedFolders: request.SelectedFolders ?? [],
            selectedFiles: request.SelectedFiles ?? [],
            excludedFolders: request.ExcludedFolders ?? [],
            excludedFiles: request.ExcludedFiles ?? [],
            workspaceEncryptionSession: workspaceEncryptionSession);

        return TypedResults.Ok(new CountThumbnailableFilesResponseDto
        {
            FileCount = countResult.FilesCount,
            TotalSizeInBytes = countResult.TotalSizeInBytes
        });
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

    private static async Task<Ok<CancelThumbnailBatchResponseDto>> CancelThumbnailBatch(
        [FromRoute] Guid batchId,
        HttpContext httpContext,
        CancelThumbnailBatchOperation cancelThumbnailBatchOperation,
        CancellationToken cancellationToken)
    {
        var cancelledCount = await cancelThumbnailBatchOperation.Execute(
            batchId: batchId,
            cancellationToken: cancellationToken);

        return TypedResults.Ok(new CancelThumbnailBatchResponseDto
        {
            CancelledCount = cancelledCount
        });
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
        IClock clock,
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
        
        // Initial state: counts + ready are computed once (this also seeds the delta tracking), and
        // the outstanding file-id set is streamed in chunks below — so the first event never carries
        // a huge (30k+) list. The client accumulates each chunk's ids into its spinner set.
        var initialSnapshot = getThumbnailGenerationStatusQuery.GetSnapshot(
            workspace,
            batchId,
            lastCompletedAt);

        var initialReady = new List<ReadyThumbnailDto>();

        var initialFresh = initialSnapshot
            .NewCompleted
            .Where(job => sentQcIds.Add(job.QcId))
            .ToList();

        GetThumbnailGenerationStatusQuery.Apply(
            initialFresh,
            failedByVariant,
            initialReady);

        if (initialSnapshot.NewCompleted.Count > 0)
            lastCompletedAt = initialSnapshot.NewCompleted.Max(job => job.CompletedAt);

        long? outstandingCursor = null;
        var sentAnyOutstandingChunk = false;

        while (true)
        {
            var page = getThumbnailGenerationStatusQuery.GetUnprocessedFileExternalIdsPage(
                batchId: batchId,
                afterQId: outstandingCursor,
                rowLimit: OutstandingChunkRowLimit);

            if (page.FileExternalIds.Count == 0)
                break;

            // First chunk also carries the ready deltas; later chunks only extend the spinner set.
            await WriteSseStatus(
                response,
                GetThumbnailGenerationStatusQuery.BuildStatus(
                    initialSnapshot.Counts,
                    failedByVariant,
                    sentAnyOutstandingChunk ? [] : initialReady,
                    page.FileExternalIds),
                cancellationToken);

            sentAnyOutstandingChunk = true;
            outstandingCursor = page.LastQId;

            if (!page.HasMore)
                break;
        }

        // No outstanding files (batch already finished/failed) — still emit one status so the client
        // gets the terminal counts and any ready deltas, then close if nothing is left to do.
        if (!sentAnyOutstandingChunk)
        {
            var terminalStatus = GetThumbnailGenerationStatusQuery.BuildStatus(
                initialSnapshot.Counts,
                failedByVariant,
                initialReady,
                []);

            await WriteSseStatus(
                response,
                terminalStatus,
                cancellationToken);

            if (terminalStatus.Pending == 0)
                return;
        }

        var keepAlive = TimeSpan.FromSeconds(20);

        // Time-based push throttle. The notifier's channel (cap=1, drop-oldest) already coalesces
        // bursts WHILE we're busy, but a fast consumer would still push per signal. 1 push/s is
        // plenty for a progress bar — anything faster the eye can't follow anyway. First push
        // (above) and final push (when Pending==0) go immediately so start and end stay snappy.
        var minPushInterval = TimeSpan.FromSeconds(1);
        var lastPushAt = clock.UtcNow;

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

            // Sleep the remainder of the throttle window. Signals arriving in the meantime get
            // coalesced into the channel's single slot — we drain them on wake and produce ONE
            // status push for the whole burst.
            var elapsed = DateTime.UtcNow - lastPushAt;

            if (elapsed < minPushInterval)
            {
                try
                {
                    await Task.Delay(
                        minPushInterval - elapsed,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                subscription.DrainPending();
            }

            // Later pushes are deltas only — readyThumbnails carries the just-completed files.
            var status = NextStatus();

            await WriteSseStatus(
                response,
                status,
                cancellationToken);

            lastPushAt = clock.UtcNow;

            if (status.Pending == 0)
                break;
        }

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

            // Deltas never carry outstanding ids — those are streamed once, in chunks, at startup.
            return GetThumbnailGenerationStatusQuery.BuildStatus(
                snapshot.Counts,
                failedByVariant,
                ready,
                []);
        }
    }

    private static async Task WriteSseStatus(
        HttpResponse response,
        ThumbnailGenerationStatusResponseDto status,
        CancellationToken cancellationToken)
    {
        await response.WriteAsync(
            text: $"data: {Json.Serialize(status)}\n\n", 
            cancellationToken: cancellationToken);

        await response.Body.FlushAsync(
            cancellationToken);
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
            var encryptionMode = workspace.GetFileEncryptionMode(
                fileEncryptionMetadata: file.EncryptionMetadata,
                workspaceEncryptionSession: workspaceEncryptionSession);

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
