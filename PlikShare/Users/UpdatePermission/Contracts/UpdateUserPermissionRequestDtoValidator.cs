using FluentValidation;
using PlikShare.Core.Authorization;

namespace PlikShare.Users.UpdatePermission.Contracts;

public class UpdateUserPermissionRequestDtoValidator : AbstractValidator<UpdateUserPermissionRequestDto>
{
    private readonly HashSet<string> _validPermissions =
    [
        Permissions.AddWorkspace,
        Permissions.ManageStorages,
        Permissions.ManageUsers,
        Permissions.ManageEmailProviders,
        Permissions.ManageGeneralSettings
    ];

    public UpdateUserPermissionRequestDtoValidator()
    {
        RuleFor(x => x.PermissionName)
            .NotEmpty()
            .Must(BeValidPermission)
            .WithMessage(x => $"Permission name must be one of: {string.Join(", ", _validPermissions)}.");
    }

    private bool BeValidPermission(string permissionName)
    {
        return _validPermissions.Contains(permissionName);
    }
}