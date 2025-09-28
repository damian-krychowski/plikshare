namespace PlikShare.Core.Authorization;

public class Permissions
{
    public const string AddWorkspace = "add:workspace";
    public const string ManageGeneralSettings = "manage:general-settings";
    public const string ManageUsers = "manage:users";
    public const string ManageStorages = "manage:storages";
    public const string ManageEmailProviders = "manage:email-providers";
    public const string ManageAuth = "manage:auth";
    public const string ManageIntegrations = "manage:integrations";

    public static readonly string[] All = [
        AddWorkspace,
        ManageGeneralSettings,
        ManageUsers,
        ManageStorages,
        ManageEmailProviders,
        ManageAuth,
        ManageIntegrations
    ];

    public static readonly string[] Admin = [
        ManageGeneralSettings,
        ManageUsers,
        ManageStorages,
        ManageEmailProviders,
        ManageAuth,
        ManageIntegrations
    ];

    public static bool IsForAdminOnly(string permission) => Admin.Contains(permission);
    public static bool IsValidPermission(string permission) => All.Contains(permission);
}