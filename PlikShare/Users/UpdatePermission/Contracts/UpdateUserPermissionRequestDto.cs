namespace PlikShare.Users.UpdatePermission.Contracts;

public record UpdateUserPermissionRequestDto(
    string PermissionName,
    UpdateUserPermissionOperation Operation);

public enum UpdateUserPermissionOperation
{
    AddPermission = 0,
    RemovePermission
}