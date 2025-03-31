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
using PlikShare.Users.UpdateDefaultMaxWorkspaceSizeInBytes;
using PlikShare.Users.UpdateDefaultMaxWorkspaceSizeInBytes.Contracts;
using PlikShare.Users.UpdateIsAdmin;
using PlikShare.Users.UpdateIsAdmin.Contracts;
using PlikShare.Users.UpdateMaxWorkspaceNumber;
using PlikShare.Users.UpdateMaxWorkspaceNumber.Contracts;
using PlikShare.Users.UpdatePermission;
using PlikShare.Users.UpdatePermission.Contracts;
using PlikShare.Users.Validation;
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
            .AddEndpointFilter<ValidateUserUpdateFilter>()
            .WithName("UpdateIsAdmin");

        group.MapPatch("/{userExternalId}/permission", UpdatePermission)
            .AddEndpointFilter<ValidateUserUpdateFilter>()
            .WithName("UpdatePermission");

        group.MapPatch("/{userExternalId}/max-workspace-number", UpdateMaxWorkspaceNumber)
            .AddEndpointFilter<ValidateUserUpdateFilter>()
            .WithName("UpdateMaxWorkspaceNumber");

        group.MapPatch("/{userExternalId}/default-max-workspace-size-in-bytes", UpdateDefaultMaxWorkspaceSizeInBytes)
            .AddEndpointFilter<ValidateUserUpdateFilter>()
            .WithName("UpdateDefaultMaxWorkspaceSizeInBytes");
    }

    private static GetUsersResponseDto GetUsers(GetUsersQuery getUsersQuery)
    {
        var response = getUsersQuery.Execute();

        return response;
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

        var response = getUserDetailsQuery.Execute(
            user: user);

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateIsAdmin(
        [FromRoute] UserExtId userExternalId,
        [FromBody] UpdateIsAdminRequestDto request,
        HttpContext httpContext,
        UserCache userCache,
        UpdateIsAdminQuery updateIsAdminQuery,
        CancellationToken cancellationToken)
    {
        var user = await userCache.TryGetUser(
            userExternalId: userExternalId,
            cancellationToken: cancellationToken);

        await updateIsAdminQuery.Execute(
            user: user!,
            isAdmin: request.IsAdmin,
            cancellationToken: cancellationToken);

        await userCache.InvalidateEntry(
            user!.Id,
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateMaxWorkspaceNumber(
        [FromRoute] UserExtId userExternalId,
        [FromBody] UpdateUserMaxWorkspaceNumberRequestDto request,
        HttpContext httpContext,
        UserCache userCache,
        UpdateUserMaxWorkspaceNumberQuery updateUserMaxWorkspaceNumberQuery,
        CancellationToken cancellationToken)
    {
        var user = await userCache.TryGetUser(
            userExternalId: userExternalId,
            cancellationToken: cancellationToken);

        await updateUserMaxWorkspaceNumberQuery.Execute(
            user: user!,
            maxWorkspaceNumber: request.MaxWorkspaceNumber,
            cancellationToken: cancellationToken);

        await userCache.InvalidateEntry(
            user!.Id,
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateDefaultMaxWorkspaceSizeInBytes(
        [FromRoute] UserExtId userExternalId,
        [FromBody] UpdateUserDefaultMaxWorkspaceSizeInBytesRequestDto request,
        HttpContext httpContext,
        UserCache userCache,
        UpdateUserDefaultMaxWorkspaceSizeInBytesQuery updateUserDefaultMaxWorkspaceSizeInBytesQuery,
        CancellationToken cancellationToken)
    {
        var user = await userCache.TryGetUser(
            userExternalId: userExternalId,
            cancellationToken: cancellationToken);

        await updateUserDefaultMaxWorkspaceSizeInBytesQuery.Execute(
            user: user!,
            defaultMaxWorkspaceSizeInBytes: request.DefaultMaxWorkspaceSizeInBytes,
            cancellationToken: cancellationToken);

        await userCache.InvalidateEntry(
            user!.Id,
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
        var user = await userCache.TryGetUser(
            userExternalId: userExternalId,
            cancellationToken: cancellationToken);

        await updateUserPermissionQuery.Execute(
            user: user!,
            operation: request.Operation switch
            {
                UpdateUserPermissionOperation.AddPermission => UpdateUserPermissionQuery.Operation.AddPermission,
                UpdateUserPermissionOperation.RemovePermission => UpdateUserPermissionQuery.Operation.RemovePermission,
                _ => throw new ArgumentOutOfRangeException(nameof(request.Operation), request.Operation, "Operation value is not recognized")
            },
            permissionName: request.PermissionName,
            cancellationToken: cancellationToken);

        await userCache.InvalidateEntry(
            user!.Id,
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