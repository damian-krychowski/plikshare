using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Boxes.Cache;
using PlikShare.BulkDelete;
using PlikShare.BulkDelete.Contracts;
using PlikShare.Core.Authorization;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Protobuf;
using PlikShare.Core.Queue;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using PlikShare.Users.Id;
using PlikShare.Users.Middleware;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.CheckBucketStatus.Contracts;
using PlikShare.Workspaces.CountSelectedItems;
using PlikShare.Workspaces.CountSelectedItems.Contracts;
using PlikShare.Workspaces.Create;
using PlikShare.Workspaces.Create.Contracts;
using PlikShare.Workspaces.Delete;
using PlikShare.Workspaces.Get.Contracts;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.Members.AcceptInvitation;
using PlikShare.Workspaces.Members.AcceptInvitation.Contracts;
using PlikShare.Workspaces.Members.CreateInvitation;
using PlikShare.Workspaces.Members.CreateInvitation.Contracts;
using PlikShare.Workspaces.Members.LeaveWorkspace;
using PlikShare.Workspaces.Members.List;
using PlikShare.Workspaces.Members.List.Contracts;
using PlikShare.Workspaces.Members.RejectInvitation;
using PlikShare.Workspaces.Members.Revoke;
using PlikShare.Workspaces.Members.UpdatePermissions;
using PlikShare.Workspaces.Members.UpdatePermissions.Contracts;
using PlikShare.Workspaces.Permissions;
using PlikShare.Workspaces.SearchFilesTree;
using PlikShare.Workspaces.SearchFilesTree.Contracts;
using PlikShare.Workspaces.UpdateName;
using PlikShare.Workspaces.UpdateName.Contracts;
using PlikShare.Workspaces.Validation;

namespace PlikShare.Workspaces;

public static class WorkspacesEndpoints
{
    public static void MapWorkspacesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workspaces")
            .WithTags("Workspaces")
            .RequireAuthorization(policyNames: AuthPolicy.Internal);

        // Basic workspace operations
        group.MapPost("/", CreateWorkspace)
            .AddEndpointFilter(new RequirePermissionEndpointFilter(Core.Authorization.Permissions.AddWorkspace))
            .WithName("CreateWorkspace");

        group.MapGet("/{workspaceExternalId}", GetWorkspaceDetails)
            .AddEndpointFilter<ValidateWorkspaceFilter>()
            .WithName("GetWorkspaceDetails");

        group.MapGet("/{workspaceExternalId}/is-bucket-created", CheckIfWorkspaceBucketWasCreated)
            .AddEndpointFilter<ValidateWorkspaceFilter>()
            .WithName("CheckIfWorkspaceBucketWasCreated");

        group.MapPatch("/{workspaceExternalId}/name", UpdateWorkspaceName)
            .AddEndpointFilter<ValidateWorkspaceFilter>()
            .WithName("UpdateWorkspaceName");

        group.MapDelete("/{workspaceExternalId}", DeleteWorkspace)
            .AddEndpointFilter<ValidateWorkspaceFilter>()
            .WithName("DeleteWorkspace");

        // Files tree operations
        group.MapPost("/{workspaceExternalId}/count-selected-items", CountSelectedItems)
            .AddEndpointFilter<ValidateWorkspaceFilter>()
            .WithName("CountSelectedItems");

        group.MapPost("/{workspaceExternalId}/search-files-tree", SearchFilesTree)
            .AddEndpointFilter<ValidateWorkspaceFilter>()
            .WithName("SearchFilesTree")
            .WithProtobufResponse();

        // Invitation operations
        group.MapPost("/{workspaceExternalId}/accept-invitation", AcceptWorkspaceInvitation)
            .WithName("AcceptWorkspaceInvitation");

        group.MapPost("/{workspaceExternalId}/reject-invitation", RejectWorkspaceInvitation)
            .WithName("RejectWorkspaceInvitation");

        // Member management
        group.MapGet("/{workspaceExternalId}/members", ListWorkspaceMembers)
            .AddEndpointFilter<ValidateWorkspaceFilter>()
            .WithName("ListWorkspaceMembers");

        group.MapPost("/{workspaceExternalId}/members", InviteMember)
            .AddEndpointFilter<ValidateWorkspaceFilter>()
            .WithName("InviteWorkspaceMember");

        group.MapDelete("/{workspaceExternalId}/members/{memberExternalId}", RevokeMember)
            .AddEndpointFilter<ValidateWorkspaceFilter>()
            .WithName("RevokeWorkspaceMember");

        group.MapPatch("/{workspaceExternalId}/members/{memberExternalId}/permissions", UpdateMemberPermissions)
            .AddEndpointFilter<ValidateWorkspaceFilter>()
            .WithName("UpdateWorkspaceMemberPermissions");

        group.MapPost("/{workspaceExternalId}/members/leave", LeaveSharedWorkspace)
            .AddEndpointFilter<ValidateWorkspaceFilter>()
            .WithName("LeaveSharedWorkspace");

        // Bulk operations
        group.MapPost("/{workspaceExternalId}/bulk-delete", BulkDelete)
            .AddEndpointFilter<ValidateWorkspaceFilter>()
            .WithName("BulkDelete");
    }

    private static SearchFilesTreeResponseDto SearchFilesTree(
        [FromBody] SearchFilesTreeRequestDto request,
        HttpContext httpContext,
        SearchFilesTreeQuery searchFilesTreeQuery)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var response = searchFilesTreeQuery.Execute(
            workspace: workspaceMembership.Workspace,
            request: request,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            boxFolderId: null);

        return response;
    }

    private static CountSelectedItemsResponseDto CountSelectedItems(
        [FromBody] CountSelectedItemsRequestDto request,
        HttpContext httpContext,
        CountSelectedItemsQuery countSelectedItemsQuery)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var response = countSelectedItemsQuery.Execute(
            workspace: workspaceMembership.Workspace,
            request: request,
            boxFolderId: null);

        return response;
    }

    private static async Task<Results<Ok<CreateWorkspaceResponseDto>, NotFound<HttpError>, BadRequest<HttpError>>> CreateWorkspace(
        [FromBody] CreateWorkspaceRequestDto request,
        HttpContext httpContext,
        CreateWorkspaceQuery createWorkspaceQuery,
        IQueue queue,
        CancellationToken cancellationToken)
    {
        var user = httpContext.GetUserContext();

        var result = await createWorkspaceQuery.Execute(
            storageExternalId: request.StorageExternalId,
            user: user,
            name: request.Name,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        if (result.Code == CreateWorkspaceQuery.ResultCode.StorageNotFound)
            return HttpErrors.Storage.NotFound(request.StorageExternalId);

        if (result.Code == CreateWorkspaceQuery.ResultCode.MaxNumberOfWorkspacesReached)
            return HttpErrors.User.MaxNumberOfWorkspacesReached(user.ExternalId, user.MaxWorkspaceNumber);

        return TypedResults.Ok(new CreateWorkspaceResponseDto
        {
            ExternalId = result.Workspace.ExternalId,
            MaxSizeInBytes = result.Workspace.MaxSizeInBytes
        });
    }

    private static async ValueTask<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> DeleteWorkspace(
        HttpContext httpContext,
        ScheduleWorkspaceDeleteQuery scheduleWorkspaceDeleteQuery,
        WorkspaceCache workspaceCache,
        BoxCache boxCache,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        if (workspaceMembership is { IsOwnedByUser: false, User.HasAdminRole: false })
            return HttpErrors.Workspace.CannotDelete(workspaceMembership.Workspace.ExternalId);

        var (resultCode, deletedBoxes) = await scheduleWorkspaceDeleteQuery.Execute(
            workspace: workspaceMembership.Workspace,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case ScheduleWorkspaceDeleteQuery.ResultCode.Ok:
                await workspaceCache.InvalidateEntry(
                    workspaceMembership.Workspace.ExternalId,
                    cancellationToken);

                await boxCache.InvalidateEntries(
                    deletedBoxes!,
                    cancellationToken);

                return TypedResults.Ok();

            case ScheduleWorkspaceDeleteQuery.ResultCode.NotFound:
                return HttpErrors.Workspace.NotFound(workspaceMembership.Workspace.ExternalId);

            case ScheduleWorkspaceDeleteQuery.ResultCode.UsedByIntegration:
                return HttpErrors.Workspace.UsedByIntegration(workspaceMembership.Workspace.ExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(ScheduleWorkspaceDeleteQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok<AcceptWorkspaceInvitationResponseDto>, NotFound<HttpError>, BadRequest<HttpError>>> AcceptWorkspaceInvitation(
        [FromRoute] WorkspaceExtId workspaceExternalId,
        HttpContext httpContext,
        WorkspaceMembershipCache workspaceMembershipCache,
        AcceptWorkspaceInvitationQuery acceptWorkspaceInvitationQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = await workspaceMembershipCache.TryGetWorkspaceMembership(
            workspaceExternalId: workspaceExternalId,
            memberId: httpContext.GetUserContext().Id,
            cancellationToken: cancellationToken);

        if (workspaceMembership is null || workspaceMembership.Workspace.IsBeingDeleted)
            return HttpErrors.Workspace.InvitationNotFound(workspaceExternalId);

        if (workspaceMembership.Invitation is { WasInvitationAccepted: true })
            return HttpErrors.Workspace.InvitationAlreadyAccepted(workspaceExternalId);

        var result = await acceptWorkspaceInvitationQuery.Execute(
            workspaceMembership: workspaceMembership,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        switch (result)
        {
            case AcceptWorkspaceInvitationQuery.ResultCode.Ok:
                await workspaceMembershipCache.InvalidateEntry(
                    workspaceMembership,
                    cancellationToken);

                return TypedResults.Ok(new AcceptWorkspaceInvitationResponseDto
                {
                    WorkspaceCurrentSizeInBytes = workspaceMembership.Workspace.CurrentSizeInBytes,
                    WorkspaceMaxSizeInBytes = workspaceMembership.Workspace.MaxSizeInBytes
                });

            case AcceptWorkspaceInvitationQuery.ResultCode.MembershipNotFound:
                return HttpErrors.Workspace.InvitationNotFound(workspaceExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(AcceptWorkspaceInvitationQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> RejectWorkspaceInvitation(
        [FromRoute] WorkspaceExtId workspaceExternalId,
        HttpContext httpContext,
        WorkspaceMembershipCache workspaceMembershipCache,
        RejectWorkspaceInvitationQuery rejectWorkspaceInvitationQuery,
        CancellationToken cancellationToken)
    {
        var user = httpContext.GetUserContext();

        var workspaceMembership = await workspaceMembershipCache.TryGetWorkspaceMembership(
            workspaceExternalId: workspaceExternalId,
            memberId: user.Id,
            cancellationToken: cancellationToken);

        if (workspaceMembership is null || workspaceMembership.Workspace.IsBeingDeleted)
            return HttpErrors.Workspace.InvitationNotFound(workspaceExternalId);

        if (workspaceMembership.Invitation is { WasInvitationAccepted: true })
            return HttpErrors.Workspace.InvitationAlreadyAccepted(workspaceExternalId);

        var result = await rejectWorkspaceInvitationQuery.Execute(
            workspaceMembership: workspaceMembership,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        return result switch
        {
            RejectWorkspaceInvitationQuery.ResultCode.Ok => TypedResults.Ok(),

            RejectWorkspaceInvitationQuery.ResultCode.MembershipNotFound => HttpErrors.Workspace.MemberNotFound(
                userId: httpContext.GetUserContext().ExternalId,
                workspaceExternalId: workspaceExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(RejectWorkspaceInvitationQuery),
                resultValueStr: result.ToString())
        };
    }

    private static CheckWorkspaceBucketStatusResponseDto CheckIfWorkspaceBucketWasCreated(
        HttpContext httpContext)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        return new CheckWorkspaceBucketStatusResponseDto(
            IsBucketCreated: workspaceMembership.Workspace.IsBucketCreated);
    }

    private static Results<Ok<GetWorkspaceDetailsResponseDto>, NotFound<HttpError>> GetWorkspaceDetails(
        HttpContext httpContext)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var integrations = workspaceMembership
            .Workspace
            .Integrations;

        return TypedResults.Ok(new GetWorkspaceDetailsResponseDto
        {
            ExternalId = workspaceMembership.Workspace.ExternalId,
            Name = workspaceMembership.Workspace.Name,
            CurrentSizeInBytes = workspaceMembership.Workspace.CurrentSizeInBytes,
            MaxSizeInBytes = workspaceMembership.Workspace.MaxSizeInBytes,
            Owner = new WorkspaceOwnerDto
            {
                ExternalId = workspaceMembership.Workspace.Owner.ExternalId,
                Email = workspaceMembership.Workspace.Owner.Email.Value
            },
            PendingUploadsCount = 0, //todo implement
            Permissions = new WorkspacePermissions(
                AllowShare: workspaceMembership.Permissions.AllowShare),
            Integrations = new WorkspaceIntegrationsDto
            {
                Textract = integrations.Textract is null
                    ? null
                    : new TextractIntegrationDetailsDto
                    {
                        ExternalId = integrations.Textract.ExternalId,
                        Name = integrations.Textract.Name
                    },

                ChatGpt = integrations
                    .ChatGpt
                    .Select(chatGpt => new ChatGptIntegrationDetailsDto
                    {
                        ExternalId = chatGpt.ExternalId,
                        Name = chatGpt.Name,
                        Models = chatGpt
                            .Models
                            .Select(model => new ChatGptModelDto
                            {
                                Alias = model.Alias,
                                MaxIncludeSizeInBytes = model.MaxIncludeSizeInBytes,
                                SupportedFileTypes = model.SupportedFileTypes.ToList()
                            })
                            .ToList(),
                        DefaultModel = chatGpt.DefaultModel
                    })
                    .ToList()
            },
            IsBucketCreated = workspaceMembership.Workspace.IsBucketCreated
        });
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateWorkspaceName(
        [FromBody] UpdateWorkspaceNameRequestDto request,
        HttpContext httpContext,
        WorkspaceCache workspaceCache,
        UpdateWorkspaceNameQuery updateWorkspaceNameQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var resultCode = await updateWorkspaceNameQuery.Execute(
            workspace: workspaceMembership.Workspace,
            name: request.Name,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateWorkspaceNameQuery.ResultCode.Ok:
                await workspaceCache.InvalidateEntry(
                    workspaceMembership.Workspace.ExternalId,
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateWorkspaceNameQuery.ResultCode.NotFound:
                return HttpErrors.Workspace.NotFound(workspaceMembership.Workspace.ExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateWorkspaceNameQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static GetWorkspaceMembersListResponseDto ListWorkspaceMembers(
        HttpContext httpContext,
        GetWorkspaceMembersListQuery getWorkspaceMembersListQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var response = getWorkspaceMembersListQuery.Execute(
            workspace: workspaceMembership.Workspace,
            cancellationToken: cancellationToken);

        return response;
    }

    private static async ValueTask<Results<Ok<CreateWorkspaceMemberInvitationResponseDto>, ForbidHttpResult>> InviteMember(
        [FromBody] CreateWorkspaceMemberInvitationRequestDto request,
        CreateWorkspaceMemberInvitationOperation createWorkspaceMemberInvitationOperation,
        HttpContext httpContext,

        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        if (!workspaceMembership.Permissions.AllowShare)
        {
            return TypedResults.Forbid();
        }

        var user = httpContext.GetUserContext();

        var result = await createWorkspaceMemberInvitationOperation.Execute(
            workspace: workspaceMembership.Workspace,
            inviter: user,
            memberEmails: request
                .MemberEmails
                .Select(email => new Email(email)),
            permission: new WorkspacePermissions(
                AllowShare: request.AllowShare),
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        return TypedResults.Ok(
            new CreateWorkspaceMemberInvitationResponseDto
            {
                Members = result
                    .Members!
                    .Select(m => new CreateWorkspaceMemberInvitationResponseDto.WorkspaceInvitationMember(
                        Email: m.Email.Value,
                        ExternalId: m.ExternalId))
                    .ToList()
            });
    }

    private static async Task<Results<Ok, NotFound<HttpError>, ForbidHttpResult>> RevokeMember(
        [FromRoute] UserExtId memberExternalId,
        HttpContext httpContext,
        UserCache userCache,
        RevokeWorkspaceMemberQuery revokeWorkspaceMemberQuery,
        WorkspaceMembershipCache workspaceMembershipCache,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        if (!workspaceMembership.Permissions.AllowShare)
        {
            return TypedResults.Forbid();
        }

        var memberToRevoke = await userCache.TryGetUser(
            userExternalId: memberExternalId,
            cancellationToken: cancellationToken);

        if (memberToRevoke is null)
            return HttpErrors.Workspace.MemberNotFound(
                memberExternalId,
                workspaceMembership.Workspace.ExternalId);

        var result = await revokeWorkspaceMemberQuery.Execute(
            workspace: workspaceMembership.Workspace,
            member: memberToRevoke,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        switch (result)
        {
            case RevokeWorkspaceMemberQuery.ResultCode.Ok:
                await workspaceMembershipCache.InvalidateEntry(
                    workspaceExternalId: workspaceMembership.Workspace.ExternalId,
                    memberId: memberToRevoke.Id,
                    cancellationToken: cancellationToken);

                return TypedResults.Ok();

            case RevokeWorkspaceMemberQuery.ResultCode.MembershipNotFound:
                return HttpErrors.Workspace.MemberNotFound(
                    memberExternalId,
                    workspaceMembership.Workspace.ExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(RevokeWorkspaceMemberQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>, ForbidHttpResult>> UpdateMemberPermissions(
        [FromRoute] UserExtId memberExternalId,
        [FromBody] UpdateWorkspaceMemberPermissionsRequestDto request,
        HttpContext httpContext,
        UserCache userCache,
        UpdateWorkspaceMemberPermissionsQuery updateWorkspaceMemberPermissionsQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        if (!workspaceMembership.Permissions.AllowShare)
        {
            return TypedResults.Forbid();
        }

        var memberToUpdate = await userCache.TryGetUser(
            userExternalId: memberExternalId,
            cancellationToken: cancellationToken);

        if (memberToUpdate is null)
            return HttpErrors.Workspace.MemberNotFound(
                memberExternalId,
                workspaceMembership.Workspace.ExternalId);

        var resultCode = await updateWorkspaceMemberPermissionsQuery.Execute(
            workspace: workspaceMembership.Workspace,
            member: memberToUpdate,
            permissions: new WorkspacePermissions(
                AllowShare: request.AllowShare),
            cancellationToken: cancellationToken);

        return resultCode switch
        {
            UpdateWorkspaceMemberPermissionsQuery.ResultCode.Ok => TypedResults.Ok(),

            UpdateWorkspaceMemberPermissionsQuery.ResultCode.NotFound => HttpErrors.Workspace.MemberNotFound(
                memberExternalId,
                workspaceMembership.Workspace.ExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(UpdateWorkspaceMemberPermissionsQuery),
                resultValueStr: resultCode.ToString())
        };
    }
    
    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> LeaveSharedWorkspace(
        HttpContext httpContext,
        LeaveSharedWorkspaceQuery leaveSharedWorkspaceQuery,
        WorkspaceMembershipCache workspaceMembershipCache,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        if (!workspaceMembership.WasUserInvited)
            return HttpErrors.Workspace.MemberNotInvited(workspaceMembership.Workspace.ExternalId);

        var user = httpContext.GetUserContext();

        var resultCode = await leaveSharedWorkspaceQuery.Execute(
            workspace: workspaceMembership.Workspace,
            member: user,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case LeaveSharedWorkspaceQuery.ResultCode.Ok:
                await workspaceMembershipCache.InvalidateEntry(
                    workspaceExternalId: workspaceMembership.Workspace.ExternalId,
                    memberId: user.Id,
                    cancellationToken: cancellationToken);

                return TypedResults.Ok();

            case LeaveSharedWorkspaceQuery.ResultCode.MembershipNotFound:
                return HttpErrors.Workspace.MemberNotFound(
                    user.ExternalId,
                    workspaceExternalId: workspaceMembership.Workspace.ExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(LeaveSharedWorkspaceQuery),
                    resultValueStr: resultCode.ToString());
        }
    }
    
    private static Task<BulkDeleteResponseDto> BulkDelete(
        [FromBody] BulkDeleteRequestDto request,
        HttpContext httpContext,
        BulkDeleteQuery bulkDeleteQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        return bulkDeleteQuery.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalIds: request.FileExternalIds.ToArray(),
            folderExternalIds: request.FolderExternalIds.ToArray(),
            fileUploadExternalIds: request.FileUploadExternalIds.ToArray(),
            boxFolderId: null,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            isFileDeleteAllowedByBoxPermissions: true,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);
    }
}