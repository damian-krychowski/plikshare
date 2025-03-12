namespace PlikShare.BoxExternalAccess.Authorization;

public static class BoxAccessDetailsHttpContextExtensions
{
    public static BoxAccess GetBoxAccess(this HttpContext httpContext)
    {
        var boxAccess =  httpContext.Items[BoxAccess.HttpContextName];

        if (boxAccess is not BoxAccess ba)
        {
            throw new InvalidOperationException(
                $"Cannot extract BoxAccess from HttpContext.");
        }

        return ba;
    }
}