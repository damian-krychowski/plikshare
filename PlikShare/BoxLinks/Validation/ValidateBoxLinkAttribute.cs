using PlikShare.BoxLinks.Cache;
using PlikShare.BoxLinks.Id;
using PlikShare.Core.Utils;

namespace PlikShare.BoxLinks.Validation;

public class ValidateBoxLinkFilter : IEndpointFilter
{
    public const string BoxLinkContext = "box-link-context";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var boxLinkExternalIdStr = context.HttpContext.Request.RouteValues["boxLinkExternalId"]?.ToString();

        if (string.IsNullOrWhiteSpace(boxLinkExternalIdStr))
            return HttpErrors.BoxLink.MissingExternalId();

        if (!BoxLinkExtId.TryParse(boxLinkExternalIdStr, null, out var boxLinkExternalId))
            return HttpErrors.BoxLink.InvalidExternalId(boxLinkExternalIdStr);

        var boxLink = await context
            .HttpContext
            .RequestServices
            .GetRequiredService<BoxLinkCache>()
            .TryGetBoxLink(
                externalId: boxLinkExternalId,
                cancellationToken: context.HttpContext.RequestAborted);

        if (boxLink is null
            or { Box.IsBeingDeleted: true }
            or { Box.Workspace.IsBeingDeleted: true })
        {
            return HttpErrors.BoxLink.NotFound(boxLinkExternalId);
        }

        context.HttpContext.Items[BoxLinkContext] = boxLink;
        return await next(context);
    }
}