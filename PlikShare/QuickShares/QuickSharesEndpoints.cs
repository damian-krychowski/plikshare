using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.AuditLog;
using PlikShare.Core.Authorization;
using PlikShare.Core.Clock;
using PlikShare.Core.Protobuf;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.QuickShares.Cache;
using Audit = PlikShare.AuditLog.Details.Audit;
using PlikShare.QuickShares.Create;
using PlikShare.QuickShares.Create.Contracts;
using PlikShare.QuickShares.Delete;
using PlikShare.QuickShares.Get;
using PlikShare.QuickShares.Get.Contracts;
using PlikShare.QuickShares.Id;
using PlikShare.QuickShares.List;
using PlikShare.QuickShares.List.Contracts;
using PlikShare.QuickShares.UpdateExpiration;
using PlikShare.QuickShares.UpdateExpiration.Contracts;
using PlikShare.QuickShares.UpdateItems;
using PlikShare.QuickShares.UpdateItems.Contracts;
using PlikShare.QuickShares.UpdateMaxDownloads;
using PlikShare.QuickShares.UpdateMaxDownloads.Contracts;
using PlikShare.QuickShares.UpdateMode;
using PlikShare.QuickShares.UpdateMode.Contracts;
using PlikShare.QuickShares.UpdateName;
using PlikShare.QuickShares.UpdateName.Contracts;
using PlikShare.QuickShares.UpdatePassword;
using PlikShare.QuickShares.UpdatePassword.Contracts;
using PlikShare.QuickShares.UpdateSlug;
using PlikShare.QuickShares.UpdateSlug.Contracts;
using PlikShare.QuickShares.Validation;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Validation;

namespace PlikShare.QuickShares;

public static class QuickSharesEndpoints
{
    public static void MapQuickSharesEndpoints(this WebApplication app)
    {
        var group = app
            .MapGroup("/api/workspaces/{workspaceExternalId}/quick-shares")
            .WithTags("QuickShares")
            .RequireAuthorization(policyNames: AuthPolicy.Internal)
            .AddEndpointFilter<ValidateWorkspaceFilter>();

        group.MapPost("/", CreateQuickShare)
            .WithName("CreateQuickShare")
            .WithProtobufResponse();

        group.MapGet("/", GetQuickShares)
            .WithName("GetQuickShares");

        group.MapGet("/{quickShareExternalId}", GetQuickShare)
            .WithName("GetQuickShare")
            .AddEndpointFilter<ValidateQuickShareFilter>();

        group.MapDelete("/{quickShareExternalId}", DeleteQuickShare)
            .WithName("DeleteQuickShare")
            .AddEndpointFilter<ValidateQuickShareFilter>();

        group.MapPatch("/{quickShareExternalId}/name", UpdateQuickShareName)
            .WithName("UpdateQuickShareName")
            .AddEndpointFilter<ValidateQuickShareFilter>();

        group.MapPatch("/{quickShareExternalId}/slug", UpdateQuickShareSlug)
            .WithName("UpdateQuickShareSlug")
            .AddEndpointFilter<ValidateQuickShareFilter>();

        group.MapPatch("/{quickShareExternalId}/expiration", UpdateQuickShareExpiration)
            .WithName("UpdateQuickShareExpiration")
            .AddEndpointFilter<ValidateQuickShareFilter>();

        group.MapPatch("/{quickShareExternalId}/password", UpdateQuickSharePassword)
            .WithName("UpdateQuickSharePassword")
            .AddEndpointFilter<ValidateQuickShareFilter>();

        group.MapPatch("/{quickShareExternalId}/max-downloads", UpdateQuickShareMaxDownloads)
            .WithName("UpdateQuickShareMaxDownloads")
            .AddEndpointFilter<ValidateQuickShareFilter>();

        group.MapPatch("/{quickShareExternalId}/mode", UpdateQuickShareMode)
            .WithName("UpdateQuickShareMode")
            .AddEndpointFilter<ValidateQuickShareFilter>();

        group.MapPatch("/{quickShareExternalId}/items", UpdateQuickShareItems)
            .WithName("UpdateQuickShareItems")
            .AddEndpointFilter<ValidateQuickShareFilter>();
    }

    private static async Task<IResult> CreateQuickShare(
        HttpContext httpContext,
        CreateQuickShareQuery createQuickShareQuery,
        QuickSharePasswordHasher passwordHasher,
        QuickShareUrlBuilder urlBuilder,
        AuditLogService auditLogService,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var request = httpContext.GetProtobufRequest<CreateQuickShareRequestDto>();

        var workspace = httpContext.GetWorkspaceMembershipDetails().Workspace;

        if (workspace.EncryptionType == StorageEncryptionType.Full)
            return HttpErrors.QuickShare.FullEncryptionNotSupportedYet();

        var name = (request.Name ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name))
            return TypedResults.BadRequest(new HttpError
            {
                Code = "quick-share-name-required",
                Message = "Quick share name is required."
            });

        var selectedFiles = request.SelectedFiles ?? [];
        var selectedFolders = request.SelectedFolders ?? [];
        var excludedFiles = request.ExcludedFiles ?? [];
        var excludedFolders = request.ExcludedFolders ?? [];

        if (selectedFiles.Count == 0 && selectedFolders.Count == 0)
            return HttpErrors.QuickShare.NoItems();

        var expiresAt = string.IsNullOrWhiteSpace(request.ExpiresAt)
            ? (DateTimeOffset?)null
            : DateTimeOffset.Parse(request.ExpiresAt);

        if (expiresAt is not null && expiresAt <= clock.UtcNow)
            return HttpErrors.QuickShare.ExpirationInThePast();

        var mode = EnumUtils.FromKebabCase<QuickShareMode>(request.Mode);

        if (request.MaxDownloads is <= 0)
            return TypedResults.BadRequest(new HttpError
            {
                Code = "quick-share-max-downloads-invalid",
                Message = "Max downloads must be greater than zero."
            });

        string? passwordHashBase64 = null;
        byte[]? passwordSalt = null;

        if (!string.IsNullOrEmpty(request.Password))
        {
            var (hash, salt) = await passwordHasher.Hash(request.Password);
            passwordHashBase64 = hash;
            passwordSalt = salt;
        }

        var creatorExternalId = httpContext.User.GetExternalId();

        var customSlug = string.IsNullOrWhiteSpace(request.CustomSlug)
            ? null
            : request.CustomSlug.Trim();

        var result = await createQuickShareQuery.Execute(
            workspace: workspace,
            creatorExternalId: creatorExternalId,
            name: name,
            customSlug: customSlug,
            selectedFiles: selectedFiles ?? [],
            selectedFolders: selectedFolders ?? [],
            excludedFiles: excludedFiles ?? [],
            excludedFolders: excludedFolders ?? [],
            mode: mode,
            allowIndividualFileDownload: request.AllowIndividualFileDownload,
            expiresAt: expiresAt,
            passwordHashBase64: passwordHashBase64,
            passwordSalt: passwordSalt,
            maxDownloads: request.MaxDownloads,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case CreateQuickShareQuery.ResultCode.Ok:
                var selectedCtx = auditLogService.GetBulkItemsContext(
                    folderExternalIds: selectedFolders,
                    fileExternalIds: selectedFiles,
                    fileUploadExternalIds: []);

                var excludedCtx = auditLogService.GetBulkItemsContext(
                    folderExternalIds: excludedFolders,
                    fileExternalIds: excludedFiles,
                    fileUploadExternalIds: []);

                await auditLogService.Log(
                    Audit.QuickShare.CreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        quickShare: new Audit.QuickShareRef
                        {
                            ExternalId = result.QuickShareExternalId,
                            Name = name
                        },
                        mode: mode,
                        allowIndividualFileDownload: request.AllowIndividualFileDownload,
                        hasPassword: passwordHashBase64 is not null,
                        maxDownloads: request.MaxDownloads,
                        expiresAt: expiresAt,
                        selectedFiles: selectedCtx.Files,
                        selectedFolders: selectedCtx.Folders,
                        excludedFiles: excludedCtx.Files,
                        excludedFolders: excludedCtx.Folders),
                    cancellationToken);

                return TypedResults.Ok(new CreateQuickShareResponseDto
                {
                    ExternalId = result.QuickShareExternalId.Value,
                    Slug = result.Slug!,
                    Url = urlBuilder.BuildUrl(result.Slug!)
                });

            case CreateQuickShareQuery.ResultCode.CreatorNotFound:
                return HttpErrors.User.NotFound(creatorExternalId);

            case CreateQuickShareQuery.ResultCode.ItemsNotFound:
                return HttpErrors.QuickShare.ItemsNotFound();

            case CreateQuickShareQuery.ResultCode.SlugInvalid:
                return HttpErrors.QuickShare.SlugFormatInvalid();

            case CreateQuickShareQuery.ResultCode.SlugTaken:
                return HttpErrors.QuickShare.SlugTaken();

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(CreateQuickShareQuery),
                    resultValueStr: result.Code.ToString());
        }
    }

    private static Ok<GetQuickSharesResponseDto> GetQuickShares(
        HttpContext httpContext,
        GetQuickSharesQuery getQuickSharesQuery)
    {
        var workspace = httpContext.GetWorkspaceMembershipDetails().Workspace;

        var response = getQuickSharesQuery.Execute(
            workspace: workspace);

        return TypedResults.Ok(response);
    }

    private static Ok<GetQuickShareResponseDto> GetQuickShare(
        HttpContext httpContext,
        GetQuickShareItemsQuery getItemsQuery,
        QuickShareUrlBuilder urlBuilder)
    {
        var quickShare = httpContext.GetQuickShareContext();

        var items = getItemsQuery.Execute(
            quickShareId: quickShare.Id);

        var hasSecret = quickShare.SecretHash is not null;

        // For FE workspaces the per-share secret token is never stored, so the full
        // URL can't be reconstructed in this view — owner saw it once at creation.
        var url = hasSecret ? null : urlBuilder.BuildUrl(quickShare.Slug);

        return TypedResults.Ok(new GetQuickShareResponseDto(
            ExternalId: quickShare.ExternalId,
            Name: quickShare.Name,
            CreatorExternalId: quickShare.CreatorExternalId,
            CreatedAt: quickShare.CreatedAt,
            ExpiresAt: quickShare.ExpiresAt,
            HasPassword: quickShare.PasswordHash is not null,
            MaxDownloads: quickShare.MaxDownloads,
            DownloadsCount: quickShare.DownloadsCount,
            Mode: quickShare.Mode,
            AllowIndividualFileDownload: quickShare.AllowIndividualFileDownload,
            LastAccessedAt: quickShare.LastAccessedAt,
            Slug: quickShare.Slug,
            HasSecret: hasSecret,
            Url: url,
            Items: items));
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> DeleteQuickShare(
        [FromRoute] QuickShareExtId quickShareExternalId,
        HttpContext httpContext,
        DeleteQuickShareQuery deleteQuickShareQuery,
        QuickShareCache quickShareCache,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var quickShare = httpContext.GetQuickShareContext();

        var resultCode = await deleteQuickShareQuery.Execute(
            quickShare: quickShare,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case DeleteQuickShareQuery.ResultCode.Ok:
                await quickShareCache.InvalidateEntry(
                    quickShareId: quickShare.Id,
                    cancellationToken: cancellationToken);

                await auditLogService.Log(
                    Audit.QuickShare.DeletedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: quickShare.Workspace.ToAuditLogWorkspaceRef(),
                        quickShare: quickShare.ToAuditLogQuickShareRef()),
                    cancellationToken);

                return TypedResults.Ok();

            case DeleteQuickShareQuery.ResultCode.NotFound:
                return HttpErrors.QuickShare.NotFound(quickShareExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(DeleteQuickShareQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok, BadRequest<HttpError>, NotFound<HttpError>>> UpdateQuickShareName(
        [FromRoute] QuickShareExtId quickShareExternalId,
        [FromBody] UpdateQuickShareNameRequestDto request,
        HttpContext httpContext,
        UpdateQuickShareNameQuery updateQuickShareNameQuery,
        QuickShareCache quickShareCache,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var quickShare = httpContext.GetQuickShareContext();

        var name = (request.Name ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name))
            return TypedResults.BadRequest(new HttpError
            {
                Code = "quick-share-name-required",
                Message = "Quick share name is required."
            });

        var resultCode = await updateQuickShareNameQuery.Execute(
            quickShare: quickShare,
            name: name,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateQuickShareNameQuery.ResultCode.Ok:
                await quickShareCache.InvalidateEntry(
                    quickShareId: quickShare.Id,
                    cancellationToken: cancellationToken);

                await auditLogService.Log(
                    Audit.QuickShare.NameUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: quickShare.Workspace.ToAuditLogWorkspaceRef(),
                        quickShare: new Audit.QuickShareRef
                        {
                            ExternalId = quickShare.ExternalId,
                            Name = name
                        }),
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateQuickShareNameQuery.ResultCode.NotFound:
                return HttpErrors.QuickShare.NotFound(quickShareExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateQuickShareNameQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<IResult> UpdateQuickShareSlug(
        [FromRoute] QuickShareExtId quickShareExternalId,
        [FromBody] UpdateQuickShareSlugRequestDto request,
        HttpContext httpContext,
        UpdateQuickShareSlugQuery updateQuickShareSlugQuery,
        QuickShareCache quickShareCache,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var quickShare = httpContext.GetQuickShareContext();

        var slug = (request.Slug ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(slug))
            return HttpErrors.QuickShare.SlugFormatInvalid();

        if (slug == quickShare.Slug)
            return Results.Ok();

        var oldSlug = quickShare.Slug;

        var resultCode = await updateQuickShareSlugQuery.Execute(
            quickShare: quickShare,
            slug: slug,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateQuickShareSlugQuery.ResultCode.Ok:
                await quickShareCache.InvalidateEntry(
                    quickShareId: quickShare.Id,
                    cancellationToken: cancellationToken);

                await auditLogService.Log(
                    Audit.QuickShare.SlugUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: quickShare.Workspace.ToAuditLogWorkspaceRef(),
                        quickShare: quickShare.ToAuditLogQuickShareRef(),
                        oldSlug: oldSlug,
                        newSlug: slug),
                    cancellationToken);

                return Results.Ok();

            case UpdateQuickShareSlugQuery.ResultCode.NotFound:
                return HttpErrors.QuickShare.NotFound(quickShareExternalId);

            case UpdateQuickShareSlugQuery.ResultCode.SlugInvalid:
                return HttpErrors.QuickShare.SlugFormatInvalid();

            case UpdateQuickShareSlugQuery.ResultCode.SlugTaken:
                return HttpErrors.QuickShare.SlugTaken();

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateQuickShareSlugQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok, BadRequest<HttpError>, NotFound<HttpError>>> UpdateQuickShareExpiration(
        [FromRoute] QuickShareExtId quickShareExternalId,
        [FromBody] UpdateQuickShareExpirationRequestDto request,
        HttpContext httpContext,
        UpdateQuickShareExpirationQuery updateQuickShareExpirationQuery,
        QuickShareCache quickShareCache,
        AuditLogService auditLogService,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var quickShare = httpContext.GetQuickShareContext();

        if (request.ExpiresAt is not null && request.ExpiresAt <= clock.UtcNow)
            return HttpErrors.QuickShare.ExpirationInThePast();

        var resultCode = await updateQuickShareExpirationQuery.Execute(
            quickShare: quickShare,
            expiresAt: request.ExpiresAt,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateQuickShareExpirationQuery.ResultCode.Ok:
                await quickShareCache.InvalidateEntry(
                    quickShareId: quickShare.Id,
                    cancellationToken: cancellationToken);

                await auditLogService.Log(
                    Audit.QuickShare.ExpirationUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: quickShare.Workspace.ToAuditLogWorkspaceRef(),
                        quickShare: quickShare.ToAuditLogQuickShareRef(),
                        expiresAt: request.ExpiresAt),
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateQuickShareExpirationQuery.ResultCode.NotFound:
                return HttpErrors.QuickShare.NotFound(quickShareExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateQuickShareExpirationQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateQuickSharePassword(
        [FromRoute] QuickShareExtId quickShareExternalId,
        [FromBody] UpdateQuickSharePasswordRequestDto request,
        HttpContext httpContext,
        UpdateQuickSharePasswordQuery updateQuickSharePasswordQuery,
        QuickSharePasswordHasher passwordHasher,
        QuickShareCache quickShareCache,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var quickShare = httpContext.GetQuickShareContext();

        string? passwordHashBase64 = null;
        byte[]? passwordSalt = null;

        if (!string.IsNullOrEmpty(request.Password))
        {
            var (hash, salt) = await passwordHasher.Hash(request.Password);
            passwordHashBase64 = hash;
            passwordSalt = salt;
        }

        var resultCode = await updateQuickSharePasswordQuery.Execute(
            quickShare: quickShare,
            passwordHashBase64: passwordHashBase64,
            passwordSalt: passwordSalt,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateQuickSharePasswordQuery.ResultCode.Ok:
                await quickShareCache.InvalidateEntry(
                    quickShareId: quickShare.Id,
                    cancellationToken: cancellationToken);

                await auditLogService.Log(
                    Audit.QuickShare.PasswordUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: quickShare.Workspace.ToAuditLogWorkspaceRef(),
                        quickShare: quickShare.ToAuditLogQuickShareRef(),
                        isSet: passwordHashBase64 is not null),
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateQuickSharePasswordQuery.ResultCode.NotFound:
                return HttpErrors.QuickShare.NotFound(quickShareExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateQuickSharePasswordQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok, BadRequest<HttpError>, NotFound<HttpError>>> UpdateQuickShareMaxDownloads(
        [FromRoute] QuickShareExtId quickShareExternalId,
        [FromBody] UpdateQuickShareMaxDownloadsRequestDto request,
        HttpContext httpContext,
        UpdateQuickShareMaxDownloadsQuery updateQuickShareMaxDownloadsQuery,
        QuickShareCache quickShareCache,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var quickShare = httpContext.GetQuickShareContext();

        if (request.MaxDownloads is <= 0)
            return TypedResults.BadRequest(new HttpError
            {
                Code = "quick-share-max-downloads-invalid",
                Message = "Max downloads must be greater than zero."
            });

        var resultCode = await updateQuickShareMaxDownloadsQuery.Execute(
            quickShare: quickShare,
            maxDownloads: request.MaxDownloads,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateQuickShareMaxDownloadsQuery.ResultCode.Ok:
                await quickShareCache.InvalidateEntry(
                    quickShareId: quickShare.Id,
                    cancellationToken: cancellationToken);

                await auditLogService.Log(
                    Audit.QuickShare.MaxDownloadsUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: quickShare.Workspace.ToAuditLogWorkspaceRef(),
                        quickShare: quickShare.ToAuditLogQuickShareRef(),
                        maxDownloads: request.MaxDownloads),
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateQuickShareMaxDownloadsQuery.ResultCode.NotFound:
                return HttpErrors.QuickShare.NotFound(quickShareExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateQuickShareMaxDownloadsQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateQuickShareMode(
        [FromRoute] QuickShareExtId quickShareExternalId,
        [FromBody] UpdateQuickShareModeRequestDto request,
        HttpContext httpContext,
        UpdateQuickShareModeQuery updateQuickShareModeQuery,
        QuickShareCache quickShareCache,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var quickShare = httpContext.GetQuickShareContext();

        var resultCode = await updateQuickShareModeQuery.Execute(
            quickShare: quickShare,
            mode: request.Mode,
            allowIndividualFileDownload: request.AllowIndividualFileDownload,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateQuickShareModeQuery.ResultCode.Ok:
                await quickShareCache.InvalidateEntry(
                    quickShareId: quickShare.Id,
                    cancellationToken: cancellationToken);

                await auditLogService.Log(
                    Audit.QuickShare.ModeUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: quickShare.Workspace.ToAuditLogWorkspaceRef(),
                        quickShare: quickShare.ToAuditLogQuickShareRef(),
                        mode: request.Mode,
                        allowIndividualFileDownload: request.AllowIndividualFileDownload),
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateQuickShareModeQuery.ResultCode.NotFound:
                return HttpErrors.QuickShare.NotFound(quickShareExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateQuickShareModeQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok, BadRequest<HttpError>, NotFound<HttpError>>> UpdateQuickShareItems(
        [FromRoute] QuickShareExtId quickShareExternalId,
        [FromBody] UpdateQuickShareItemsRequestDto request,
        HttpContext httpContext,
        UpdateQuickShareItemsQuery updateQuickShareItemsQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var quickShare = httpContext.GetQuickShareContext();

        var selectedFiles = request.SelectedFiles ?? [];
        var selectedFolders = request.SelectedFolders ?? [];
        var excludedFiles = request.ExcludedFiles ?? [];
        var excludedFolders = request.ExcludedFolders ?? [];

        var resultCode = await updateQuickShareItemsQuery.Execute(
            quickShare: quickShare,
            selectedFiles: selectedFiles,
            selectedFolders: selectedFolders,
            excludedFiles: excludedFiles,
            excludedFolders: excludedFolders,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateQuickShareItemsQuery.ResultCode.Ok:
                var selectedCtx = auditLogService.GetBulkItemsContext(
                    folderExternalIds: selectedFolders,
                    fileExternalIds: selectedFiles,
                    fileUploadExternalIds: []);

                var excludedCtx = auditLogService.GetBulkItemsContext(
                    folderExternalIds: excludedFolders,
                    fileExternalIds: excludedFiles,
                    fileUploadExternalIds: []);

                await auditLogService.Log(
                    Audit.QuickShare.ItemsUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: quickShare.Workspace.ToAuditLogWorkspaceRef(),
                        quickShare: quickShare.ToAuditLogQuickShareRef(),
                        selectedFiles: selectedCtx.Files,
                        selectedFolders: selectedCtx.Folders,
                        excludedFiles: excludedCtx.Files,
                        excludedFolders: excludedCtx.Folders),
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateQuickShareItemsQuery.ResultCode.NoItems:
                return HttpErrors.QuickShare.NoItems();

            case UpdateQuickShareItemsQuery.ResultCode.ItemsNotFound:
                return HttpErrors.QuickShare.ItemsNotFound();

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateQuickShareItemsQuery),
                    resultValueStr: resultCode.ToString());
        }
    }
}
