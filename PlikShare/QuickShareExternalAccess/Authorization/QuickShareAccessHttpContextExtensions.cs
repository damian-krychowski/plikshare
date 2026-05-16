namespace PlikShare.QuickShareExternalAccess.Authorization;

public static class QuickShareAccessHttpContextExtensions
{
    public static QuickShareAccess GetQuickShareAccess(this HttpContext httpContext)
    {
        var entry = httpContext.Items[QuickShareAccess.HttpContextName];

        if (entry is not QuickShareAccess access)
            throw new InvalidOperationException("Cannot extract QuickShareAccess from HttpContext.");

        return access;
    }
}
