using PlikShare.Users.PermissionsAndRoles;
using PlikShare.Users.StorageAccess;

namespace PlikShare.GeneralSettings.Contracts;

public class GetApplicationSettingsResponse
{
    public required string ApplicationSignUp { get; init; }
    public required string? TermsOfService { get; init; }
    public required string? PrivacyPolicy { get; init; }
    public required string? ApplicationName { get; init; }
    public required List<AppSettings.SignUpCheckbox> SignUpCheckboxes { get; init; }
    public required int? NewUserDefaultMaxWorkspaceNumber { get; init; }
    public required long? NewUserDefaultMaxWorkspaceSizeInBytes { get; init; }
    public required int? NewUserDefaultMaxWorkspaceTeamMembers { get; init; }
    public required UserPermissionsAndRolesDto NewUserDefaultPermissionsAndRoles { get; init; }
    public required bool AlertOnNewUserRegistered { get; init; }
    public required NewUserDefaultStorageAccessDto NewUserDefaultStorageAccess { get; init; }
}

public class NewUserDefaultStorageAccessDto
{
    public required UserStorageAccessMode Mode { get; init; }
    public required List<string> StorageExternalIds { get; init; }
}

