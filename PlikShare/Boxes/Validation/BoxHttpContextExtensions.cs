using PlikShare.Boxes.Cache;

namespace PlikShare.Boxes.Validation;

public static class BoxHttpContextExtensions
{
    public static BoxContext GetBoxContext(this HttpContext httpContext)
    {
        var box =  httpContext.Items[ValidateBoxFilter.BoxContext];

        if (box is not BoxContext context)
        {
            throw new InvalidOperationException(
                $"Cannot extract BoxContext from HttpContext.");
        }

        return context;
    }
}