using PlikShare.Core.Authorization;

namespace PlikShare.Users.PermissionsAndRoles;

public class UserPermissionsAndRolesDto
{
    public required bool IsAdmin { get; init; }

    public required bool CanAddWorkspace { get; init; }
    public required bool CanManageGeneralSettings { get; init; }
    public required bool CanManageUsers { get; init; }
    public required bool CanManageStorages { get; init; }
    public required bool CanManageEmailProviders { get; init; }
}

public static class UserPermissionsAndRolesDtoExtensions
{
    public static List<string> GetPermissionsList(this UserPermissionsAndRolesDto dto)
    {
        var result = new List<string>();

        if (dto.CanAddWorkspace)
            result.Add(Permissions.AddWorkspace);

        if (dto.CanManageEmailProviders)
            result.Add(Permissions.ManageEmailProviders);

        if (dto.CanManageGeneralSettings)
            result.Add(Permissions.ManageGeneralSettings);

        if (dto.CanManageStorages)
            result.Add(Permissions.ManageStorages);

        if (dto.CanManageUsers)
            result.Add(Permissions.ManageUsers);

        return result;
    }

    public static List<string> GetPermissionsAndRolesList(this UserPermissionsAndRolesDto dto)
    {
        var result = dto.GetPermissionsList();

        if (dto.IsAdmin)
            result.Add(Roles.Admin);

        return result;
    }
}