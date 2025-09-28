using System.ComponentModel;
using PlikShare.Users.Entities;
using PlikShare.Users.Id;

namespace PlikShare.Users.Cache;

[ImmutableObject(true)]
public sealed class UserContext
{
    public required UserStatus Status { get; init; }
    public required int Id { get; init; }
    public required UserExtId ExternalId { get; init; }
    public required Email Email { get; init; }
    public required bool IsEmailConfirmed { get; init; }
    public required UserSecurityStamps Stamps { get; init; }
    public required UserRoles Roles { get; init; }
    public required UserPermissions Permissions { get; init; }
    public UserInvitation? Invitation { get; init; }
    public int? MaxWorkspaceNumber { get; init; }
    public long? DefaultMaxWorkspaceSizeInBytes { get; init; }
    public int? DefaultMaxWorkspaceTeamMembers { get; init; }

    public bool HasAdminRole => Roles.IsAppOwner || Roles.IsAdmin;
}

public enum UserStatus
{
    Invitation = 0,
    Registered
}

[ImmutableObject(true)]
public sealed class UserRoles
{
    public required bool IsAppOwner { get; init; }
    public required bool IsAdmin { get; init; }
}

[ImmutableObject(true)]
public sealed class UserPermissions
{
    public required bool CanAddWorkspace { get; init; }
    public required bool CanManageGeneralSettings { get; init; }
    public required bool CanManageUsers { get; init; }
    public required bool CanManageStorages { get; init; }
    public required bool CanManageEmailProviders { get; init; }
    public required bool CanManageAuth { get; init; }
    public required bool CanManageIntegrations { get; init; }
}

[ImmutableObject(true)]
public sealed class UserInvitation
{
    public required string Code { get; init; }
}

[ImmutableObject(true)]
public sealed class UserSecurityStamps
{
    public required string Security { get; init; }
    public required string Concurrency { get; init; }
}
