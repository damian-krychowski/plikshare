using PlikShare.Core.Authorization;
using PlikShare.Users.Cache;
using PlikShare.Users.Entities;

namespace PlikShare.Users.Middleware;

public static class UserHttpContextExtensions
{
    public const string UserContext = "user-context";    
    
    public static UserContext GetUserContext(this HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(UserContext, out var httpContextItem) 
            && httpContextItem is UserContext userContext)
        {
            return userContext;
        }

        //when user is present in httpcontext it means that it must have already accepted an invitation
        //or registered himself, thus the status is always Registered at this moment and Invitation is always NULL
        var context = new UserContext(
            Status: UserStatus.Registered,
            Id: httpContext.User.GetDatabaseId(),
            ExternalId: httpContext.User.GetExternalId(),
            Email: new Email(httpContext.User.GetEmail()),
            IsEmailConfirmed: true,
            Stamps: new UserSecurityStamps(
                Security: httpContext.User.GetSecurityStamp(),
                Concurrency: httpContext.User.GetConcurrencyStamp()),
            Roles: new UserRoles(
                IsAppOwner: httpContext.User.GetIsAppOwner(),
                IsAdmin: httpContext.User.IsInRole(Roles.Admin)),
            Permissions: new UserPermissions(
                CanAddWorkspace: httpContext.User.HasPermission(Permissions.AddWorkspace),
                CanManageGeneralSettings: httpContext.User.HasPermission(Permissions.ManageGeneralSettings),
                CanManageUsers: httpContext.User.HasPermission(Permissions.ManageUsers),
                CanManageStorages: httpContext.User.HasPermission(Permissions.ManageStorages),
                CanManageEmailProviders: httpContext.User.HasPermission(Permissions.ManageEmailProviders)),
            Invitation: null,
            MaxWorkspaceNumber: httpContext.User.GetMaxWorkspaceNumber(),
            DefaultMaxWorkspaceSizeInBytes: httpContext.User.GetDefaultMaxWorkspaceSizeInBytes());

        httpContext.Items[UserContext] = context;

        return context;
    }
}