using System.Security.Cryptography;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.AuditLog;
using PlikShare.Core.Clock;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.QuickShareExternalAccess.Authorization;
using PlikShare.QuickShareExternalAccess.Contracts;
using PlikShare.QuickShareExternalAccess.GetBulkDownloadLink;
using PlikShare.QuickShareExternalAccess.GetContent;
using PlikShare.QuickShareExternalAccess.GetFileDownloadLink;
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
        HttpContext httpContext,
        GenerateQuickShareBulkDownloadLinkOperation generateLinkOperation,
        TrackQuickShareDownloadQuery trackDownloadQuery,
        QuickShareCache quickShareCache,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var access = httpContext.GetQuickShareAccess();
        var correlationId = httpContext.GetCorrelationId();

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

        var preSignedUrl = generateLinkOperation.Execute(
            quickShare: access.QuickShare,
            userIdentity: access.UserIdentity);

        if (!access.IsOwnerPreview)
        {
            await auditLogService.Log(
                Audit.QuickShare.BulkDownloadLinkGeneratedEntry(
                    actor: access.ToAuditLogActorContext(correlationId),
                    workspace: access.QuickShare.Workspace.ToAuditLogWorkspaceRef(),
                    quickShare: access.QuickShare.ToAuditLogQuickShareRef(),
                    downloadsCountAfter: access.QuickShare.DownloadsCount + 1),
                cancellationToken);
        }

        return Results.Ok(new GetQuickShareBulkDownloadLinkResponseDto(
            PreSignedUrl: preSignedUrl));
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
}
