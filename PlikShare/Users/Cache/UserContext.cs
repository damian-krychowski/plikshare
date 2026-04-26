using PlikShare.Core.Encryption;
using PlikShare.Users.Entities;
using PlikShare.Users.Id;
using System.ComponentModel;

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
    public required bool HasPassword { get; init; }
    public required int? MaxWorkspaceNumber { get; init; }
    public required long? DefaultMaxWorkspaceSizeInBytes { get; init; }
    public required int? DefaultMaxWorkspaceTeamMembers { get; init; }
    public required UserEncryptionMetadata? EncryptionMetadata { get; init; }
    
    public bool HasAdminRole => Roles.IsAppOwner || Roles.IsAdmin;
    public bool IsEncryptionConfigured => EncryptionMetadata is not null;
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
    public required bool CanManageAuditLog { get; init; }
}

[ImmutableObject(true)]
public sealed class UserSecurityStamps
{
    public required string Security { get; init; }
    public required string Concurrency { get; init; }
}

[ImmutableObject(true)]
public sealed class UserEncryptionMetadata
{
    public required byte[] PublicKey { get; init; }
    public required byte[] EncryptedPrivateKey { get; init; }
    public required byte[] KdfSalt { get; init; }
    public required EncryptionPasswordKdf.Params KdfParams { get; init; }
    public required byte[] VerifyHash { get; init; }
    public required byte[] RecoveryWrappedPrivateKey { get; init; }
    public required byte[] RecoveryVerifyHash { get; init; }
}

public class InvitationCode
{
    public required string Value { get; init; }
}