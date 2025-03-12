using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Core.Utils;

namespace PlikShare.Boxes.Validation;

public class ValidateBoxFilter : IEndpointFilter
{
    public const string BoxContext = "box-context";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var boxExternalIdStr = context.HttpContext.Request.RouteValues["boxExternalId"]?.ToString();

        if (string.IsNullOrWhiteSpace(boxExternalIdStr))
            return HttpErrors.Box.MissingExternalId();

        if (!BoxExtId.TryParse(boxExternalIdStr, null, out var boxExternalId))
            return HttpErrors.Box.InvalidExternalId(boxExternalIdStr);

        var box = await context
            .HttpContext
            .RequestServices
            .GetRequiredService<BoxCache>()
            .TryGetBox(
                boxExternalId,
                context.HttpContext.RequestAborted);

        if (box is null or { IsBeingDeleted: true })
            return HttpErrors.Box.NotFound(boxExternalId);

        context.HttpContext.Items[BoxContext] = box;

        return await next(context);
    }
}