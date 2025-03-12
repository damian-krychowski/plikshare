namespace PlikShare.Core.Authorization;

public class RequirePermissionAttribute(string permission) 
    : RequireClaimAttribute("permission", permission);