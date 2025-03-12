using System.ComponentModel;
using PlikShare.Users.Entities;
using PlikShare.Users.Id;

namespace PlikShare.Users.Cache;

[ImmutableObject(true)]
public sealed record UserContext(
    UserStatus Status,
    int Id,
    UserExtId ExternalId,
    Email Email,
    bool IsEmailConfirmed,
    UserSecurityStamps Stamps,
    UserRoles Roles,
    UserPermissions Permissions,
    UserInvitation? Invitation)
{
    public bool HasAdminRole => Roles.IsAppOwner || Roles.IsAdmin;
}

public enum UserStatus
{
    Invitation = 0,
    Registered
}

[ImmutableObject(true)]
public sealed record UserRoles(
    bool IsAppOwner,
    bool IsAdmin);

[ImmutableObject(true)]
public sealed record UserPermissions(
    bool CanAddWorkspace,
    bool CanManageGeneralSettings,
    bool CanManageUsers,
    bool CanManageStorages,
    bool CanManageEmailProviders);

[ImmutableObject(true)]
public sealed record UserInvitation(
    string Code);

[ImmutableObject(true)]
public sealed record UserSecurityStamps(
    string Security,
    string Concurrency);