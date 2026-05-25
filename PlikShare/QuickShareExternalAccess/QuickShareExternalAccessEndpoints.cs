using System.Security.Cryptography;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.AuditLog;
using PlikShare.Core.Clock;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.Protobuf;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.QuickShareExternalAccess.Authorization;
using PlikShare.QuickShareExternalAccess.Contracts;
using PlikShare.QuickShareExternalAccess.GetBulkDownloadLink;
using PlikShare.QuickShareExternalAccess.GetContent;
using PlikShare.QuickShareExternalAccess.GetFileDownloadLink;
using PlikShare.QuickShareExternalAccess.GetZipBulkDownloadLink;
using PlikShare.QuickShareExternalAccess.GetZipContentDownloadLink;
using PlikShare.QuickShareExternalAccess.GetZipFileDetails;
using PlikShare.QuickShares;
using PlikShare.QuickShares.Cache;
using PlikShare.QuickShares.TrackDownload;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.QuickShareExternalAccess;

public static class QuickShareExternalAccessEndpoints
{
    public static void MapQuickShareExternalAccessEndpoints(this WebApplication app)
    {
        var group = app
            .MapGroup("/api/quick-shares")
            .WithTags("QuickShareExternalAccess")
            .AllowAnonymous();

        group.MapGet("/{slug}/info", GetInfo)
            .WithName("QuickShareExternalAccess_GetInfo");

        group.MapPost("/{slug}/unlock", Unlock)
            .WithName("QuickShareExternalAccess_Unlock");

        group.MapGet("/{slug}/content", GetContent)
            .WithName("QuickShareExternalAccess_GetContent")
            .AddEndpointFilter<ValidateQuickShareAccessFilter>();

        group.MapPost("/{slug}/bulk-download-link", GetBulkDownloadLink)
            .WithName("QuickShareExternalAccess_GetBulkDownloadLink")
            .AddEndpointFilter<ValidateQuickShareAccessFilter>();

        group.MapGet("/{slug}/files/{fileExternalId}/download-link", GetFileDownloadLink)
            .WithName("QuickShareExternalAccess_GetFileDownloadLink")
            .AddEndpointFilter<ValidateQuickShareAccessFilter>();

        group.MapGet("/{slug}/files/{fileExternalId}/preview-link", GetFilePreviewLink)
            .WithName("QuickShareExternalAccess_GetFilePreviewLink")
            .AddEndpointFilter<ValidateQuickShareAccessFilter>();

        group.MapGet("/{slug}/files/{fileExternalId}/zip-details", GetZipFilePreviewDetails)
            .WithName("QuickShareExternalAccess_GetZipFilePreviewDetails")
            .AddEndpointFilter<ValidateQuickShareAccessFilter>()
            .WithProtobufResponse();

        group.MapPost("/{slug}/files/{fileExternalId}/zip-content-preview-link", GetZipContentPreviewLink)
            .WithName("QuickShareExternalAccess_GetZipContentPreviewLink")
            .AddEndpointFilter<ValidateQuickShareAccessFilter>();

        group.MapPost("/{slug}/files/{fileExternalId}/zip-content-download-link", GetZipContentDownloadLink)
            .WithName("QuickShareExternalAccess_GetZipContentDownloadLink")
            .AddEndpointFilter<ValidateQuickShareAccessFilter>();

        group.MapPost("/{slug}/files/{fileExternalId}/zip-bulk-download-link", GetZipBulkDownloadLink)
            .WithName("QuickShareExternalAccess_GetZipBulkDownloadLink")
            .AddEndpointFilter<ValidateQuickShareAccessFilter>();
    }

    private static async Task<IResult> GetInfo(
        [FromRoute] string slug,
        [FromQuery] string? token,
        HttpContext httpContext,
        QuickShareCache quickShareCache,
        QuickShareUnlockSession unlockSession,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return HttpErrors.QuickShare.InvalidSlug();

        var quickShare = await quickShareCache.TryGetQuickShareBySlug(
            slug: slug,
            cancellationToken: cancellationToken);

        if (quickShare is null || quickShare.Workspace.IsBeingDeleted)
            return HttpErrors.QuickShare.InvalidSlug();

        if (quickShare.SecretHash is not null)
        {
            if (string.IsNullOrEmpty(token))
                return HttpErrors.QuickShare.SecretRequired();

            var providedHash = QuickShareCache.HashSecret(token);
            if (!CryptographicOperations.FixedTimeEquals(providedHash, quickShare.SecretHash))
                return HttpErrors.QuickShare.InvalidSecret();
        }

        var session = unlockSession.ReadOrCreate(
            httpContext: httpContext,
            quickShareId: quickShare.Id);

        var isOwnerPreview = ValidateQuickShareAccessFilter.IsOwnerPreview(httpContext, quickShare);

        var requiresPassword = quickShare.PasswordHash is not null;
        var isUnlocked = !requiresPassword || unlockSession.IsUnlockValid(session);

        var isExpired = !isOwnerPreview && quickShare.ExpiresAt is { } expiresAt && expiresAt <= clock.UtcNow;
        var isExhausted = !isOwnerPreview && quickShare.MaxDownloads is { } max && quickShare.DownloadsCount >= max;

        return Results.Ok(new GetQuickShareInfoResponseDto(
            Name: quickShare.Name,
            Mode: quickShare.Mode,
            AllowIndividualFileDownload: quickShare.AllowIndividualFileDownload,
            RequiresPassword: requiresPassword,
            IsUnlocked: isUnlocked,
            IsExpired: isExpired,
            IsExhausted: isExhausted,
            IsOwnerPreview: isOwnerPreview,
            ExpiresAt: quickShare.ExpiresAt,
            MaxDownloads: quickShare.MaxDownloads,
            DownloadsCount: quickShare.DownloadsCount));
    }

    private static async Task<IResult> Unlock(
        [FromRoute] string slug,
        [FromQuery] string? token,
        [FromBody] UnlockQuickShareRequestDto request,
        HttpContext httpContext,
        QuickShareCache quickShareCache,
        QuickShareUnlockSession unlockSession,
        QuickSharePasswordHasher passwordHasher,
        AuditLogService auditLogService,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return HttpErrors.QuickShare.InvalidSlug();

        var quickShare = await quickShareCache.TryGetQuickShareBySlug(
            slug: slug,
            cancellationToken: cancellationToken);

        if (quickShare is null || quickShare.Workspace.IsBeingDeleted)
            return HttpErrors.QuickShare.InvalidSlug();

        if (quickShare.SecretHash is not null)
        {
            if (string.IsNullOrEmpty(token))
                return HttpErrors.QuickShare.SecretRequired();

            var providedHash = QuickShareCache.HashSecret(token);
            if (!CryptographicOperations.FixedTimeEquals(providedHash, quickShare.SecretHash))
                return HttpErrors.QuickShare.InvalidSecret();
        }

        if (quickShare.ExpiresAt is { } expiresAt && expiresAt <= clock.UtcNow)
            return HttpErrors.QuickShare.Expired();

        if (quickShare.MaxDownloads is { } max && quickShare.DownloadsCount >= max)
            return HttpErrors.QuickShare.Exhausted();

        if (quickShare.PasswordHash is null || quickShare.PasswordSalt is null)
            return HttpErrors.QuickShare.WrongPassword();

        var session = unlockSession.ReadOrCreate(
            httpContext: httpContext,
            quickShareId: quickShare.Id);

        var actor = new AuditLogActorContext(
            Identity: new Core.UserIdentity.QuickShareSessionUserIdentity(session.SessionId),
            Email: null,
            Ip: httpContext.Connection.RemoteIpAddress?.ToString(),
            CorrelationId: httpContext.GetCorrelationId());

        var isValid = await passwordHasher.Verify(
            password: request.Password ?? string.Empty,
            expectedHashBase64: quickShare.PasswordHash,
            salt: quickShare.PasswordSalt);

        if (!isValid)
        {
            await auditLogService.Log(
                Audit.QuickShare.UnlockFailedEntry(
                    actor: actor,
                    workspace: quickShare.Workspace.ToAuditLogWorkspaceRef(),
                    quickShare: quickShare.ToAuditLogQuickShareRef()),
                cancellationToken);

            return HttpErrors.QuickShare.WrongPassword();
        }

        unlockSession.MarkUnlocked(
            httpContext: httpContext,
            current: session);

        await auditLogService.Log(
            Audit.QuickShare.UnlockedEntry(
                actor: actor,
                workspace: quickShare.Workspace.ToAuditLogWorkspaceRef(),
                quickShare: quickShare.ToAuditLogQuickShareRef()),
            cancellationToken);

        return Results.Ok();
    }

    private static Ok<GetQuickShareContentResponseDto> GetContent(
        HttpContext httpContext,
        GetQuickShareContentOperation getContentOperation)
    {
        var access = httpContext.GetQuickShareAccess();

        var response = getContentOperation.Execute(
            quickShare: access.QuickShare);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> GetBulkDownloadLink(
        [FromBody] GetQuickShareBulkDownloadLinkRequestDto? request,
        HttpContext httpContext,
        GenerateQuickShareBulkDownloadLinkOperation generateLinkOperation,
        TrackQuickShareDownloadQuery trackDownloadQuery,
        QuickShareCache quickShareCache,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var access = httpContext.GetQuickShareAccess();
        var correlationId = httpContext.GetCorrelationId();

        // Generate the URL FIRST so EmptySelection / validation failures don't burn
        // a download-count slot. Tracking + audit only happen on a successful link.
        var result = generateLinkOperation.Execute(
            quickShare: access.QuickShare,
            userIdentity: access.UserIdentity,
            request: request);

        if (result.Code == GenerateQuickShareBulkDownloadLinkOperation.ResultCode.EmptySelection)
            return HttpErrors.QuickShare.EmptyBulkSelection();

        if (!access.IsOwnerPreview)
        {
            var trackResult = await trackDownloadQuery.Execute(
                quickShare: access.QuickShare,
                cancellationToken: cancellationToken);

            if (trackResult == TrackQuickShareDownloadQuery.ResultCode.LimitReached)
            {
                await auditLogService.Log(
                    Audit.QuickShare.DownloadLimitReachedEntry(
                        actor: access.ToAuditLogActorContext(correlationId),
                        workspace: access.QuickShare.Workspace.ToAuditLogWorkspaceRef(),
                        quickShare: access.QuickShare.ToAuditLogQuickShareRef()),
                    cancellationToken);

                return HttpErrors.QuickShare.Exhausted();
            }

            await quickShareCache.InvalidateEntry(
                quickShareId: access.QuickShare.Id,
                cancellationToken: cancellationToken);

            await auditLogService.Log(
                Audit.QuickShare.BulkDownloadLinkGeneratedEntry(
                    actor: access.ToAuditLogActorContext(correlationId),
                    workspace: access.QuickShare.Workspace.ToAuditLogWorkspaceRef(),
                    quickShare: access.QuickShare.ToAuditLogQuickShareRef(),
                    downloadsCountAfter: access.QuickShare.DownloadsCount + 1),
                cancellationToken);
        }

        return Results.Ok(new GetQuickShareBulkDownloadLinkResponseDto(
            PreSignedUrl: result.PreSignedUrl!));
    }

    private static async Task<IResult> GetFileDownloadLink(
        [FromRoute] FileExtId fileExternalId,
        [FromQuery] string contentDisposition,
        HttpContext httpContext,
        GenerateQuickShareFileDownloadLinkOperation generateLinkOperation,
        TrackQuickShareDownloadQuery trackDownloadQuery,
        QuickShareCache quickShareCache,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var access = httpContext.GetQuickShareAccess();
        var correlationId = httpContext.GetCorrelationId();

        if (!access.QuickShare.AllowIndividualFileDownload)
            return HttpErrors.QuickShare.IndividualFileDownloadDisabled();

        if (!ContentDispositionHelper.TryParse(contentDisposition, out var contentDispositionType))
            return HttpErrors.File.InvalidContentDisposition(contentDisposition);

        if (!access.IsOwnerPreview)
        {
            var trackResult = await trackDownloadQuery.Execute(
                quickShare: access.QuickShare,
                cancellationToken: cancellationToken);

            if (trackResult == TrackQuickShareDownloadQuery.ResultCode.LimitReached)
            {
                await auditLogService.Log(
                    Audit.QuickShare.DownloadLimitReachedEntry(
                        actor: access.ToAuditLogActorContext(correlationId),
                        workspace: access.QuickShare.Workspace.ToAuditLogWorkspaceRef(),
                        quickShare: access.QuickShare.ToAuditLogQuickShareRef()),
                    cancellationToken);

                return HttpErrors.QuickShare.Exhausted();
            }

            await quickShareCache.InvalidateEntry(
                quickShareId: access.QuickShare.Id,
                cancellationToken: cancellationToken);
        }

        var result = await generateLinkOperation.Execute(
            quickShare: access.QuickShare,
            fileExternalId: fileExternalId,
            contentDisposition: contentDispositionType,
            userIdentity: access.UserIdentity,
            enforceInternalPassThrough: false,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case GenerateQuickShareFileDownloadLinkOperation.ResultCode.Ok:
                if (!access.IsOwnerPreview)
                {
                    await auditLogService.LogWithFileContext(
                        fileExternalId: fileExternalId,
                        buildEntry: fileRef => Audit.QuickShare.FileDownloadLinkGeneratedEntry(
                            actor: access.ToAuditLogActorContext(correlationId),
                            workspace: access.QuickShare.Workspace.ToAuditLogWorkspaceRef(),
                            quickShare: access.QuickShare.ToAuditLogQuickShareRef(),
                            file: fileRef,
                            downloadsCountAfter: access.QuickShare.DownloadsCount + 1),
                        cancellationToken);
                }

                return Results.Ok(new GetQuickShareFileDownloadLinkResponseDto(
                    DownloadPreSignedUrl: result.DownloadPreSignedUrl!));

            case GenerateQuickShareFileDownloadLinkOperation.ResultCode.FileNotInShare:
                return HttpErrors.QuickShare.FileNotInShare();

            case GenerateQuickShareFileDownloadLinkOperation.ResultCode.FileNotFound:
                return HttpErrors.File.NotFound(fileExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(GenerateQuickShareFileDownloadLinkOperation),
                    resultValueStr: result.Code.ToString());
        }
    }

    // Preview link is intentionally separate from download-link: it always uses
    // inline content-disposition, never increments DownloadsCount, and never burns
    // the MaxDownloads quota. Owner-preview is also un-tracked on download-link,
    // but anonymous viewers should be able to look at a PDF/image without each
    // glance counting as a download.
    private static async Task<IResult> GetFilePreviewLink(
        [FromRoute] FileExtId fileExternalId,
        HttpContext httpContext,
        GenerateQuickShareFileDownloadLinkOperation generateLinkOperation,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var access = httpContext.GetQuickShareAccess();
        var correlationId = httpContext.GetCorrelationId();

        if (!access.QuickShare.AllowIndividualFileDownload)
            return HttpErrors.QuickShare.IndividualFileDownloadDisabled();

        var result = await generateLinkOperation.Execute(
            quickShare: access.QuickShare,
            fileExternalId: fileExternalId,
            contentDisposition: ContentDispositionType.Inline,
            userIdentity: access.UserIdentity,
            enforceInternalPassThrough: false,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case GenerateQuickShareFileDownloadLinkOperation.ResultCode.Ok:
                if (!access.IsOwnerPreview)
                {
                    await auditLogService.LogWithFileContext(
                        fileExternalId: fileExternalId,
                        buildEntry: fileRef => Audit.QuickShare.FilePreviewLinkGeneratedEntry(
                            actor: access.ToAuditLogActorContext(correlationId),
                            workspace: access.QuickShare.Workspace.ToAuditLogWorkspaceRef(),
                            quickShare: access.QuickShare.ToAuditLogQuickShareRef(),
                            file: fileRef),
                        cancellationToken);
                }

                return Results.Ok(new GetQuickShareFileDownloadLinkResponseDto(
                    DownloadPreSignedUrl: result.DownloadPreSignedUrl!));

            case GenerateQuickShareFileDownloadLinkOperation.ResultCode.FileNotInShare:
                return HttpErrors.QuickShare.FileNotInShare();

            case GenerateQuickShareFileDownloadLinkOperation.ResultCode.FileNotFound:
                return HttpErrors.File.NotFound(fileExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(GenerateQuickShareFileDownloadLinkOperation),
                    resultValueStr: result.Code.ToString());
        }
    }

    // Zip browsing inside a quick share: list entries in a .zip that is part of
    // the share. Listing is metadata-only — no presigned URL, no quota burn, no
    // audit (consistent with /content listing).
    private static async Task<IResult> GetZipFilePreviewDetails(
        [FromRoute] FileExtId fileExternalId,
        HttpContext httpContext,
        GenerateQuickShareZipFileDetailsOperation operation,
        CancellationToken cancellationToken)
    {
        var access = httpContext.GetQuickShareAccess();

        if (!access.QuickShare.AllowIndividualFileDownload)
            return HttpErrors.QuickShare.IndividualFileDownloadDisabled();

        var result = await operation.Execute(
            quickShare: access.QuickShare,
            fileExternalId: fileExternalId,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GenerateQuickShareZipFileDetailsOperation.ResultCode.Ok =>
                Results.Ok(result.Response!),

            GenerateQuickShareZipFileDetailsOperation.ResultCode.FileNotInShare =>
                HttpErrors.QuickShare.FileNotInShare(),

            GenerateQuickShareZipFileDetailsOperation.ResultCode.FileNotFound =>
                HttpErrors.File.NotFound(fileExternalId),

            GenerateQuickShareZipFileDetailsOperation.ResultCode.WrongFileExtension =>
                HttpErrors.File.WrongFileExtension(fileExternalId, ".zip"),

            GenerateQuickShareZipFileDetailsOperation.ResultCode.ZipFileBroken =>
                HttpErrors.File.ZipFileBroken(fileExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(GenerateQuickShareZipFileDetailsOperation),
                resultValueStr: result.Code.ToString())
        };
    }

    // Inline-preview of a single entry inside a zip — analogue of /preview-link
    // for a normal file: no quota burn, lightweight FilePreviewLinkGenerated
    // audit. The zip entry itself isn't surfaced in audit (file ref stays the
    // .zip) — sufficient for v1.
    private static async Task<IResult> GetZipContentPreviewLink(
        [FromRoute] FileExtId fileExternalId,
        [FromBody] PlikShare.Files.Preview.GetZipContentDownloadLink.Contracts.GetZipContentDownloadLinkRequestDto request,
        HttpContext httpContext,
        GenerateQuickShareZipContentDownloadLinkOperation operation,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var access = httpContext.GetQuickShareAccess();
        var correlationId = httpContext.GetCorrelationId();

        if (!access.QuickShare.AllowIndividualFileDownload)
            return HttpErrors.QuickShare.IndividualFileDownloadDisabled();

        var result = operation.Execute(
            quickShare: access.QuickShare,
            fileExternalId: fileExternalId,
            zipFile: request.Item,
            contentDisposition: ContentDispositionType.Inline,
            userIdentity: access.UserIdentity,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case GenerateQuickShareZipContentDownloadLinkOperation.ResultCode.Ok:
                if (!access.IsOwnerPreview)
                {
                    await auditLogService.LogWithFileContext(
                        fileExternalId: fileExternalId,
                        buildEntry: fileRef => Audit.QuickShare.FilePreviewLinkGeneratedEntry(
                            actor: access.ToAuditLogActorContext(correlationId),
                            workspace: access.QuickShare.Workspace.ToAuditLogWorkspaceRef(),
                            quickShare: access.QuickShare.ToAuditLogQuickShareRef(),
                            file: fileRef),
                        cancellationToken);
                }

                return Results.Ok(new PlikShare.Files.Preview.GetZipContentDownloadLink.Contracts.GetZipContentDownloadLinkResponseDto(
                    DownloadPreSignedUrl: result.DownloadPreSignedUrl!));

            case GenerateQuickShareZipContentDownloadLinkOperation.ResultCode.FileNotInShare:
                return HttpErrors.QuickShare.FileNotInShare();

            case GenerateQuickShareZipContentDownloadLinkOperation.ResultCode.FileNotFound:
                return HttpErrors.File.NotFound(fileExternalId);

            case GenerateQuickShareZipContentDownloadLinkOperation.ResultCode.WrongFileExtension:
                return HttpErrors.File.WrongFileExtension(fileExternalId, ".zip");

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(GenerateQuickShareZipContentDownloadLinkOperation),
                    resultValueStr: result.Code.ToString());
        }
    }

    // Attachment download of a single zip entry — analogue of /download-link.
    // Burns one quota slot, logs FileDownloadLinkGenerated. Mirrors the same
    // exhaustion / quota-track flow as the existing single-file download path.
    private static async Task<IResult> GetZipContentDownloadLink(
        [FromRoute] FileExtId fileExternalId,
        [FromBody] PlikShare.Files.Preview.GetZipContentDownloadLink.Contracts.GetZipContentDownloadLinkRequestDto request,
        HttpContext httpContext,
        GenerateQuickShareZipContentDownloadLinkOperation operation,
        TrackQuickShareDownloadQuery trackDownloadQuery,
        QuickShareCache quickShareCache,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var access = httpContext.GetQuickShareAccess();
        var correlationId = httpContext.GetCorrelationId();

        if (!access.QuickShare.AllowIndividualFileDownload)
            return HttpErrors.QuickShare.IndividualFileDownloadDisabled();

        if (!access.IsOwnerPreview)
        {
            var trackResult = await trackDownloadQuery.Execute(
                quickShare: access.QuickShare,
                cancellationToken: cancellationToken);

            if (trackResult == TrackQuickShareDownloadQuery.ResultCode.LimitReached)
            {
                await auditLogService.Log(
                    Audit.QuickShare.DownloadLimitReachedEntry(
                        actor: access.ToAuditLogActorContext(correlationId),
                        workspace: access.QuickShare.Workspace.ToAuditLogWorkspaceRef(),
                        quickShare: access.QuickShare.ToAuditLogQuickShareRef()),
                    cancellationToken);

                return HttpErrors.QuickShare.Exhausted();
            }

            await quickShareCache.InvalidateEntry(
                quickShareId: access.QuickShare.Id,
                cancellationToken: cancellationToken);
        }

        var result = operation.Execute(
            quickShare: access.QuickShare,
            fileExternalId: fileExternalId,
            zipFile: request.Item,
            contentDisposition: request.ContentDisposition,
            userIdentity: access.UserIdentity,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case GenerateQuickShareZipContentDownloadLinkOperation.ResultCode.Ok:
                if (!access.IsOwnerPreview)
                {
                    await auditLogService.LogWithFileContext(
                        fileExternalId: fileExternalId,
                        buildEntry: fileRef => Audit.QuickShare.FileDownloadLinkGeneratedEntry(
                            actor: access.ToAuditLogActorContext(correlationId),
                            workspace: access.QuickShare.Workspace.ToAuditLogWorkspaceRef(),
                            quickShare: access.QuickShare.ToAuditLogQuickShareRef(),
                            file: fileRef,
                            downloadsCountAfter: access.QuickShare.DownloadsCount + 1),
                        cancellationToken);
                }

                return Results.Ok(new PlikShare.Files.Preview.GetZipContentDownloadLink.Contracts.GetZipContentDownloadLinkResponseDto(
                    DownloadPreSignedUrl: result.DownloadPreSignedUrl!));

            case GenerateQuickShareZipContentDownloadLinkOperation.ResultCode.FileNotInShare:
                return HttpErrors.QuickShare.FileNotInShare();

            case GenerateQuickShareZipContentDownloadLinkOperation.ResultCode.FileNotFound:
                return HttpErrors.File.NotFound(fileExternalId);

            case GenerateQuickShareZipContentDownloadLinkOperation.ResultCode.WrongFileExtension:
                return HttpErrors.File.WrongFileExtension(fileExternalId, ".zip");

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(GenerateQuickShareZipContentDownloadLinkOperation),
                    resultValueStr: result.Code.ToString());
        }
    }

    // Bulk download of selected zip entries — analogue of /bulk-download-link.
    // Burns one quota slot regardless of how many entries are selected; the
    // generated archive arrives as a single download.
    private static async Task<IResult> GetZipBulkDownloadLink(
        [FromRoute] FileExtId fileExternalId,
        [FromBody] PlikShare.Files.Preview.GetZipBulkDownloadLink.Contracts.GetZipBulkDownloadLinkRequestDto request,
        HttpContext httpContext,
        GenerateQuickShareZipBulkDownloadLinkOperation operation,
        TrackQuickShareDownloadQuery trackDownloadQuery,
        QuickShareCache quickShareCache,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var access = httpContext.GetQuickShareAccess();
        var correlationId = httpContext.GetCorrelationId();

        if (!access.QuickShare.AllowIndividualFileDownload)
            return HttpErrors.QuickShare.IndividualFileDownloadDisabled();

        // Generate the URL FIRST so empty-selection / wrong-extension failures
        // don't burn a quota slot. Tracking only runs after a successful link.
        var result = operation.Execute(
            quickShare: access.QuickShare,
            fileExternalId: fileExternalId,
            request: request,
            userIdentity: access.UserIdentity);

        if (result.Code != GenerateQuickShareZipBulkDownloadLinkOperation.ResultCode.Ok)
        {
            return result.Code switch
            {
                GenerateQuickShareZipBulkDownloadLinkOperation.ResultCode.FileNotInShare =>
                    HttpErrors.QuickShare.FileNotInShare(),

                GenerateQuickShareZipBulkDownloadLinkOperation.ResultCode.FileNotFound =>
                    HttpErrors.File.NotFound(fileExternalId),

                GenerateQuickShareZipBulkDownloadLinkOperation.ResultCode.WrongFileExtension =>
                    HttpErrors.File.WrongFileExtension(fileExternalId, ".zip"),

                GenerateQuickShareZipBulkDownloadLinkOperation.ResultCode.EmptySelection =>
                    HttpErrors.File.EmptyZipBulkSelection(fileExternalId),

                _ => throw new UnexpectedOperationResultException(
                    operationName: nameof(GenerateQuickShareZipBulkDownloadLinkOperation),
                    resultValueStr: result.Code.ToString())
            };
        }

        if (!access.IsOwnerPreview)
        {
            var trackResult = await trackDownloadQuery.Execute(
                quickShare: access.QuickShare,
                cancellationToken: cancellationToken);

            if (trackResult == TrackQuickShareDownloadQuery.ResultCode.LimitReached)
            {
                await auditLogService.Log(
                    Audit.QuickShare.DownloadLimitReachedEntry(
                        actor: access.ToAuditLogActorContext(correlationId),
                        workspace: access.QuickShare.Workspace.ToAuditLogWorkspaceRef(),
                        quickShare: access.QuickShare.ToAuditLogQuickShareRef()),
                    cancellationToken);

                return HttpErrors.QuickShare.Exhausted();
            }

            await quickShareCache.InvalidateEntry(
                quickShareId: access.QuickShare.Id,
                cancellationToken: cancellationToken);

            await auditLogService.Log(
                Audit.QuickShare.BulkDownloadLinkGeneratedEntry(
                    actor: access.ToAuditLogActorContext(correlationId),
                    workspace: access.QuickShare.Workspace.ToAuditLogWorkspaceRef(),
                    quickShare: access.QuickShare.ToAuditLogQuickShareRef(),
                    downloadsCountAfter: access.QuickShare.DownloadsCount + 1),
                cancellationToken);
        }

        return Results.Ok(new PlikShare.Files.Preview.GetZipBulkDownloadLink.Contracts.GetZipBulkDownloadLinkResponseDto(
            DownloadPreSignedUrl: result.DownloadPreSignedUrl!));
    }
}
