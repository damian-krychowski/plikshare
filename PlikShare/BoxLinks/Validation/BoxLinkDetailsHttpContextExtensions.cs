using PlikShare.BoxLinks.Cache;

namespace PlikShare.BoxLinks.Validation;

public static class BoxLinkDetailsHttpContextExtensions
{
    public static BoxLinkContext GetBoxLinkContext(this HttpContext httpContext)
    {
        var boxLink =  httpContext.Items[ValidateBoxLinkFilter.BoxLinkContext];

        if (boxLink is not BoxLinkContext context)
        {
            throw new InvalidOperationException(
                $"Cannot extract BoxContext from HttpContext.");
        }

        return context;
    }
}