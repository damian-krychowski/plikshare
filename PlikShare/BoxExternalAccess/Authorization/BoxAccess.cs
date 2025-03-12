using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Permissions;
using PlikShare.Core.UserIdentity;

namespace PlikShare.BoxExternalAccess.Authorization;

public record BoxAccess(
    bool IsEnabled,
    BoxContext Box,
    BoxPermissions Permissions, 
    IUserIdentity UserIdentity)
{
    public const string HttpContextName = "BoxAccess";
    public bool IsOff => !IsEnabled || Box.Folder is null;
    
    public bool IsAccessedThroughLink => UserIdentity is BoxLinkSessionUserIdentity;
    
    public bool IsBoxOwnedByUser()
    {
        if (UserIdentity is UserIdentity userIdentity)
            return userIdentity.UserExternalId == Box.Workspace.Owner.ExternalId;

        return false;
    }
}