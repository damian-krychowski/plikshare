namespace PlikShare.Core.Authorization;

public class RequireAdminPermissionAttribute(string permission) 
    : RequireAdminClaimAttribute("permission", permission);