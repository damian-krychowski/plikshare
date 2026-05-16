using PlikShare.Core.Utils;
using PlikShare.QuickShares.Cache;
using PlikShare.QuickShares.Id;

namespace PlikShare.QuickShares.Validation;

public class ValidateQuickShareFilter : IEndpointFilter
{
    public const string QuickShareContextKey = "quick-share-context";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var quickShareExternalIdStr = context.HttpContext.Request.RouteValues["quickShareExternalId"]?.ToString();

        if (string.IsNullOrWhiteSpace(quickShareExternalIdStr))
            return HttpErrors.QuickShare.MissingExternalId();

        if (!QuickShareExtId.TryParse(quickShareExternalIdStr, null, out var quickShareExternalId))
            return HttpErrors.QuickShare.InvalidExternalId(quickShareExternalIdStr);

        var quickShare = await context
            .HttpContext
            .RequestServices
            .GetRequiredService<QuickShareCache>()
            .TryGetQuickShare(
                externalId: quickShareExternalId,
                cancellationToken: context.HttpContext.RequestAborted);

        if (quickShare is null || quickShare.Workspace.IsBeingDeleted)
            return HttpErrors.QuickShare.NotFound(quickShareExternalId);

        context.HttpContext.Items[QuickShareContextKey] = quickShare;
        return await next(context);
    }
}
