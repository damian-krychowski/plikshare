using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Details;
using PlikShare.Core.Authorization;
using PlikShare.Core.Encryption;
using PlikShare.Core.Protobuf;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Folders.Create;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;
using PlikShare.Folders.List;
using PlikShare.Folders.List.Contracts;
using PlikShare.Folders.MoveToFolder;
using PlikShare.Folders.MoveToFolder.Contracts;
using PlikShare.Folders.Rename;
using PlikShare.Folders.Rename.Contracts;
using PlikShare.Storages.Encryption.Authorization;
using PlikShare.Workspaces.Validation;

namespace PlikShare.Folders;

public static class FoldersEndpoints
{
    public static void MapFoldersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceExternalId}/folders")
            .WithTags("Folders")
            .RequireAuthorization(policyNames: AuthPolicy.Internal)
            .AddEndpointFilter<ValidateWorkspaceFilter>()
            .AddEndpointFilter<ValidateWorkspaceEncryptionSessionFilter>();

        group.MapPost("/", CreateFolder)
            .WithName("CreateFolder");

        group.MapPost("/bulk", BulkCreateFolder)
            .WithName("BulkCreateFolder")
            .WithProtobufResponse();

        group.MapGet("/", GetTopFolders)
            .WithName("GetTopFolders")
            .WithProtobufResponse();

        group.MapGet("/{folderExternalId}", GetFolder)
            .WithName("GetFolder")
            .WithProtobufResponse();

        group.MapPatch("/{folderExternalId}/name", UpdateFolderName)
            .WithName("UpdateFolderName");

        group.MapPatch("/move-items", MoveItemsToFolder)
            .WithName("MoveItemsToFolder");
    }

    private static async Task<Results<Ok<BulkCreateFolderResponseDto>, BadRequest<HttpError>, NotFound<HttpError>>> BulkCreateFolder(
        HttpContext httpContext,
        GetOrCreateFolderQuery getOrCreateFolderQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var request = httpContext.GetProtobufRequest<BulkCreateFolderRequestDto>();

        FolderExtId? parentFolderExternalId = request.ParentExternalId is null
            ? null
            : FolderExtId.Parse(request.ParentExternalId);

        var result = await getOrCreateFolderQuery.Execute(
            workspace: workspaceMembership.Workspace,
            parentFolderExternalId: parentFolderExternalId,
            boxFolderId: null,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            folderTreeItems: request.FolderTrees ?? [],
            ensureUniqueNames: request.EnsureUniqueNames,
            workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession(),
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case GetOrCreateFolderQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Folder.BulkCreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                        folders: result.CreatedFolders.ToAuditLogFolderRefs(
                            httpContext.TryGetWorkspaceEncryptionSession())),
                    cancellationToken);

                return TypedResults.Ok(
                    result.Response);

            case GetOrCreateFolderQuery.ResultCode.ParentFolderNotFound:
                return HttpErrors.Folder.NotFound(
                    parentFolderExternalId!.Value);

            case GetOrCreateFolderQuery.ResultCode.DuplicatedNamesFound:
                return HttpErrors.Folder.DuplicatedNamesOnInput(
                    result.TemporaryIdsWithDuplications ?? []);

            case GetOrCreateFolderQuery.ResultCode.DuplicatedTemporaryIds:
                return HttpErrors.Folder.DuplicatedTemporaryIds(
                    result.TemporaryIdsWithDuplications ?? []);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(GetOrCreateFolderQuery),
                    resultValueStr: result.Code.ToString());
        }
    }

    private static async Task<Results<Ok<CreateFolderResponseDto>, NotFound<HttpError>>> CreateFolder(
        [FromBody] CreateFolderRequestDto request,
        HttpContext httpContext,
        CreateFolderQuery createFolderQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = await createFolderQuery.Execute(
            workspace: workspaceMembership.Workspace,
            folderExternalId: request.ExternalId,
            parentFolderExternalId: request.ParentExternalId,
            name: httpContext.ToEncryptable(value: request.Name),
            boxFolderId: null,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            cancellationToken: cancellationToken);

        switch (result)
        {
            case CreateFolderQuery.ResultCode.Ok:
                await auditLogService.LogWithFolderContext(
                    folderExternalId: request.ExternalId,
                    buildEntry: folderRef => Audit.Folder.CreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                        folder: folderRef),
                    cancellationToken);

                return TypedResults.Ok(new CreateFolderResponseDto
                {
                    ExternalId = request.ExternalId
                });

            case CreateFolderQuery.ResultCode.ParentFolderNotFound:
                return HttpErrors.Folder.NotFound(
                    request.ParentExternalId!.Value);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(CreateFolderQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static GetTopFolderContentResponseDto GetTopFolders(
        HttpContext httpContext,
        GetTopFolderContentQuery getTopFolderContentQuery)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var response = getTopFolderContentQuery.Execute(
            workspace: workspaceMembership.Workspace,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession());

        return response;
    }

    private static Results<Ok<GetFolderContentResponseDto>, NotFound<HttpError>> GetFolder(
        [FromRoute] FolderExtId folderExternalId,
        HttpContext httpContext,
        GetFolderContentQuery getFolderContentQuery)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var response = getFolderContentQuery.Execute(
            workspace: workspaceMembership.Workspace,
            folderExternalId: folderExternalId,
            boxFolderId: null,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            executionFlags: new GetFolderContentQuery.ExecutionFlags(
                GetCurrentFolder: true,
                GetSubfolders: true,
                GetFiles: GetFolderContentQuery.FilesExecutionFlag.All,
                GetUploads: true),
            workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession());

        return response switch
        {
            null => HttpErrors.Folder.NotFound(folderExternalId),
            _ => TypedResults.Ok(response)
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateFolderName(
        [FromRoute] FolderExtId folderExternalId,
        [FromBody] UpdateFolderNameRequestDto request,
        HttpContext httpContext,
        UpdateFolderNameQuery updateFolderNameQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var resultCode = await updateFolderNameQuery.Execute(
            workspace: workspaceMembership.Workspace,
            folderExternalId: folderExternalId,
            name: httpContext.ToEncryptable(request.Name),
            boxFolderId: null,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            isOperationAllowedByBoxPermissions: true,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateFolderNameQuery.ResultCode.Ok:
                await auditLogService.LogWithFolderContext(
                    folderExternalId: folderExternalId,
                    buildEntry: folderRef => Audit.Folder.NameUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                        folder: folderRef),
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateFolderNameQuery.ResultCode.FolderNotFound:
                return HttpErrors.Folder.NotFound(
                    folderExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateFolderNameQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> MoveItemsToFolder(
        [FromBody] MoveItemsToFolderRequestDto request,
        HttpContext httpContext,
        MoveItemsToFolderQuery moveItemsToFolderQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var itemsContext = auditLogService.GetItemsMovedContext(
            destinationFolderExternalId: request.DestinationFolderExternalId,
            folderExternalIds: request.FolderExternalIds.ToList(),
            fileExternalIds: request.FileExternalIds.ToList(),
            fileUploadExternalIds: request.FileUploadExternalIds.ToList());

        var resultCode = await moveItemsToFolderQuery.Execute(
            workspace: workspaceMembership.Workspace,
            folderExternalIds: request.FolderExternalIds,
            fileExternalIds: request.FileExternalIds,
            fileUploadExternalIds: request.FileUploadExternalIds,
            destinationFolderExternalId: request.DestinationFolderExternalId,
            boxFolderId: null,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case MoveItemsToFolderQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Folder.ItemsMovedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                        destinationFolder: itemsContext.DestinationFolder,
                        folders: itemsContext.Folders,
                        files: itemsContext.Files,
                        fileUploads: itemsContext.FileUploads),
                    cancellationToken);

                return TypedResults.Ok();

            case MoveItemsToFolderQuery.ResultCode.DestinationFolderNotFound:
                return HttpErrors.Folder.NotFound(
                    request.DestinationFolderExternalId!.Value);

            case MoveItemsToFolderQuery.ResultCode.FoldersNotFound:
                return HttpErrors.Folder.SomeFolderNotFound();

            case MoveItemsToFolderQuery.ResultCode.FilesNotFound:
                return HttpErrors.Folder.SomeFileNotFound();

            case MoveItemsToFolderQuery.ResultCode.UploadsNotFound:
                return HttpErrors.Folder.SomeFileUploadNotFound();

            case MoveItemsToFolderQuery.ResultCode.FoldersMovedToOwnSubfolder:
                return HttpErrors.Folder.CannotMoveFoldersToOwnSubfolders();

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(MoveItemsToFolderQuery),
                    resultValueStr: resultCode.ToString());
        }
    }
}