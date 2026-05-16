using PlikShare.QuickShares.Cache;

namespace PlikShare.QuickShares.Validation;

public static class QuickShareHttpContextExtensions
{
    public static QuickShareContext GetQuickShareContext(this HttpContext httpContext)
    {
        var entry = httpContext.Items[ValidateQuickShareFilter.QuickShareContextKey];

        if (entry is not QuickShareContext context)
            throw new InvalidOperationException("Cannot extract QuickShareContext from HttpContext.");

        return context;
    }
}
