using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Boxes.Cache;
using PlikShare.Core.Authorization;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.Utils;
using PlikShare.Users.Cache;
using PlikShare.Users.Delete;
using PlikShare.Users.Entities;
using PlikShare.Users.GetDetails;
using PlikShare.Users.GetDetails.Contracts;
using PlikShare.Users.Id;
using PlikShare.Users.Invite;
using PlikShare.Users.Invite.Contracts;
using PlikShare.Users.List;
using PlikShare.Users.List.Contracts;
using PlikShare.Users.Middleware;
using PlikShare.Users.UpdateIsAdmin;
using PlikShare.Users.UpdateIsAdmin.Contracts;
using PlikShare.Users.UpdatePermission;
using PlikShare.Users.UpdatePermission.Contracts;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Users;

public static class UsersEndpoints
{
    public static void MapUsersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Policy = AuthPolicy.Internal,
                Roles = $"{Roles.Admin}"
            })
            .AddEndpointFilter(new RequireAdminPermissionEndpointFilter(Permissions.ManageUsers));

        // Basic user operations
        group.MapGet("/", GetUsers)
            .WithName("GetUsers");

        group.MapPost("/", InviteUsers)
            .WithName("InviteUsers");

        group.MapGet("/{userExternalId}", GetUserDetails)
            .WithName("GetUserDetails");

        group.MapDelete("/{userExternalId}", DeleteUser)
            .WithName("DeleteUser");

        // Update operations
        group.MapPatch("/{userExternalId}/is-admin", UpdateIsAdmin)
            .AddEndpointFilter<RequireAppOwnerEndpointFilter>()
            .WithName("UpdateIsAdmin");

        group.MapPatch("/{userExternalId}/permission", UpdatePermission)
            .WithName("UpdatePermission");
    }

    private static GetUsersResponseDto GetUsers(GetUsersQuery getUsersQuery)
    {
        var users = getUsersQuery.Execute();

        return new GetUsersResponseDto(
            Items: users
                .Select(u => new GetUsersItemDto(
                    ExternalId: u.ExternalId,
                    Email: u.Email,
                    IsEmailConfirmed: u.IsEmailConfirmed,
                    WorkspacesCount: u.WorkspacesCount,
                    Roles: new GetUserItemRolesDto(
                        IsAppOwner: u.IsAppOwner,
                        IsAdmin: u.IsAdmin),
                    Permissions: new GetUserItemPermissionsDto(
                        CanAddWorkspace: u.CanAddWorkspace,
                        CanManageGeneralSettings: u.CanManageGeneralSettings,
                        CanManageUsers: u.CanManageUsers,
                        CanManageStorages: u.CanManageStorages,
                        CanManageEmailProviders: u.CanManageEmailProviders)))
                .ToArray());
    }

    private static async Task<InviteUsersResponseDto> InviteUsers(
        [FromBody] InviteUsersRequestDto request,
        HttpContext httpContext,
        InviteUsersQuery inviteUsersQuery,
        CancellationToken cancellationToken)
    {
        var result = await inviteUsersQuery.Execute(
            emails: request.Emails.Select(x => new Email(x)).ToList(),
            inviter: httpContext.GetUserContext(),
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        return new InviteUsersResponseDto(
            Users: result
                .Select(user => new InvitedUserDto(
                    Email: user.Email.Value,
                    ExternalId: user.ExternalId))
                .ToList());
    }

    private static async ValueTask<Results<Ok<GetUserDetails.ResponseDto>, NotFound<HttpError>>> GetUserDetails(
        [FromRoute] UserExtId userExternalId,
        UserCache userCache,
        GetUserDetailsQuery getUserDetailsQuery,
        CancellationToken cancellationToken)
    {
        var user = await userCache.TryGetUser(
            userExternalId: userExternalId,
            cancellationToken: cancellationToken);

        if (user is null)
            return HttpErrors.User.NotFound(userExternalId);

        var result = getUserDetailsQuery.Execute(
            user: user);

        return TypedResults.Ok(new GetUserDetails.ResponseDto(
            User: new GetUserDetails.UserDetailsDto(
                ExternalId: user.ExternalId,
                Email: user.Email.Value,
                IsEmailConfirmed: user.IsEmailConfirmed,
                Roles: new GetUserDetails.UserRolesDto(
                    IsAppOwner: user.Roles.IsAppOwner,
                    IsAdmin: user.Roles.IsAdmin),
                Permissions: new GetUserDetails.UserPermissionsDto(
                    CanAddWorkspace: user.Permissions.CanAddWorkspace,
                    CanManageGeneralSettings: user.Permissions.CanManageGeneralSettings,
                    CanManageUsers: user.Permissions.CanManageUsers,
                    CanManageStorages: user.Permissions.CanManageStorages,
                    CanManageEmailProviders: user.Permissions.CanManageEmailProviders)),

            Workspaces: result.Workspaces
                .Select(w => new GetUserDetails.WorkspaceDto(
                    ExternalId: w.ExternalId,
                    Name: w.Name,
                    StorageName: w.StorageName,
                    CurrentSizeInBytes: w.CurrentSizeInBytes,
                    IsUsedByIntegration: w.IsUsedByIntegration,
                    IsBucketCreated: w.IsBucketCreated))
                .ToList(),

            SharedWorkspaces: result.SharedWorkspaces
                .Select(w => new GetUserDetails.SharedWorkspaceDto(
                    ExternalId: w.ExternalId,
                    Name: w.Name,
                    StorageName: w.StorageName,
                    CurrentSizeInBytes: w.CurrentSizeInBytes,
                    Owner: new GetUserDetails.UserDto(
                        ExternalId: w.OwnerExternalId,
                        Email: w.OwnerEmail),
                    Inviter: new GetUserDetails.UserDto(
                        ExternalId: w.InviterExternalId,
                        Email: w.InviterEmail),
                    WasInvitationAccepted: w.WasInvitationAccepted,
                    Permissions: new GetUserDetails.WorkspacePermissionsDto(
                        AllowShare: w.AllowShare),
                    IsUsedByIntegration: w.IsUsedByIntegration,
                    IsBucketCreated: w.IsBucketCreated))
                .ToList(),

            SharedBoxes: result.SharedBoxes
                .Select(b => new GetUserDetails.SharedBoxDto(
                    WorkspaceExternalId: b.WorkspaceExternalId,
                    WorkspaceName: b.WorkspaceName,
                    StorageName: b.StorageName,
                    Owner: new GetUserDetails.UserDto(
                        ExternalId: b.OwnerExternalId,
                        Email: b.OwnerEmail),
                    BoxExternalId: b.BoxExternalId,
                    BoxName: b.BoxName,
                    Inviter: new GetUserDetails.UserDto(
                        ExternalId: b.InviterExternalId,
                        Email: b.InviterEmail),
                    WasInvitationAccepted: b.WasInvitationAccepted,
                    Permissions: new GetUserDetails.BoxPermissionsDto(
                        AllowDownload: b.AllowDownload,
                        AllowUpload: b.AllowUpload,
                        AllowList: b.AllowList,
                        AllowDeleteFile: b.AllowDeleteFile,
                        AllowRenameFile: b.AllowRenameFile,
                        AllowMoveItems: b.AllowMoveItems,
                        AllowCreateFolder: b.AllowCreateFolder,
                        AllowRenameFolder: b.AllowRenameFolder,
                        AllowDeleteFolder: b.AllowDeleteFolder)))
                .ToList()));
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateIsAdmin(
        [FromRoute] UserExtId userExternalId,
        [FromBody] UpdateIsAdminRequestDto request,
        HttpContext httpContext,
        UserCache userCache,
        UpdateIsAdminQuery updateIsAdminQuery,
        CancellationToken cancellationToken)
    {
        if (httpContext.GetUserContext().ExternalId == userExternalId)
            return HttpErrors.User.CannotModifyOwnUser(userExternalId);

        var user = await userCache.TryGetUser(
            userExternalId: userExternalId,
            cancellationToken: cancellationToken);

        if (user is null)
            return HttpErrors.User.NotFound(userExternalId);

        await updateIsAdminQuery.Execute(
            user: user,
            isAdmin: request.IsAdmin,
            cancellationToken: cancellationToken);

        await userCache.InvalidateEntry(
            user.Id,
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdatePermission(
        [FromRoute] UserExtId userExternalId,
        [FromBody] UpdateUserPermissionRequestDto request,
        HttpContext httpContext,
        UserCache userCache,
        UpdateUserPermissionQuery updateUserPermissionQuery,
        CancellationToken cancellationToken)
    {
        var userContext = httpContext.GetUserContext();

        if (userContext.ExternalId == userExternalId)
            return HttpErrors.User.CannotModifyOwnUser(userExternalId);

        var user = await userCache.TryGetUser(
            userExternalId: userExternalId,
            cancellationToken: cancellationToken);

        if (user is null)
            return HttpErrors.User.NotFound(userExternalId);

        if (user.Roles.IsAdmin && !userContext.Roles.IsAppOwner)
            return HttpErrors.User.CannotModifyAdminUser(userExternalId);

        await updateUserPermissionQuery.Execute(
            user: user,
            operation: request.Operation switch
            {
                UpdateUserPermissionOperation.AddPermission => UpdateUserPermissionQuery.Operation.AddPermission,
                UpdateUserPermissionOperation.RemovePermission => UpdateUserPermissionQuery.Operation.RemovePermission,
                _ => throw new ArgumentOutOfRangeException(nameof(request.Operation), request.Operation, "Operation value is not recognized")
            },
            permissionName: request.PermissionName,
            cancellationToken: cancellationToken);

        await userCache.InvalidateEntry(
            user.Id,
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> DeleteUser(
        [FromRoute] UserExtId userExternalId,
        UserCache userCache,
        DeleteUserQuery deleteUserQuery,
        WorkspaceMembershipCache workspaceMembershipCache,
        BoxMembershipCache boxMembershipCache,
        CancellationToken cancellationToken)
    {
        var user = await userCache.TryGetUser(
            userExternalId: userExternalId,
            cancellationToken: cancellationToken);

        if (user is null)
            return HttpErrors.User.NotFound(userExternalId);

        var result = await deleteUserQuery.Execute(
            user: user,
            cancellationToken: cancellationToken);

        if (result.Code == DeleteUserQuery.ResultCode.NotFound)
            return HttpErrors.User.NotFound(userExternalId);

        if (result.Code == DeleteUserQuery.ResultCode.UserHasOutstandingDependencies)
            return HttpErrors.User.CannotDeleteUserWithDependencies(userExternalId);

        await userCache.InvalidateEntry(
            user.Id,
            cancellationToken: cancellationToken);

        foreach (var workspaceId in result?.DeletedWorkspaceMemberships ?? [])
        {
            await workspaceMembershipCache.InvalidateEntry(
                workspaceId: workspaceId,
                memberId: user.Id,
                cancellationToken: cancellationToken);
        }

        foreach (var boxId in result?.DeletedBoxMemberships ?? [])
        {
            await boxMembershipCache.InvalidateEntry(
                boxId: boxId,
                memberId: user.Id,
                cancellationToken: cancellationToken);
        }

        return TypedResults.Ok();
    }
}