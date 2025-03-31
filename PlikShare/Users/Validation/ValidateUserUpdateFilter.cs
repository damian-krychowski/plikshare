using PlikShare.Core.Utils;
using PlikShare.Users.Cache;
using PlikShare.Users.Id;
using PlikShare.Users.Middleware;

namespace PlikShare.Users.Validation;

public class ValidateUserUpdateFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var userExternalIdStr = context
            .HttpContext
            .Request
            .RouteValues["userExternalId"]
            ?.ToString();

        if (string.IsNullOrWhiteSpace(userExternalIdStr))
            return HttpErrors.User.MissingExternalId();

        if (!UserExtId.TryParse(userExternalIdStr, null, out var userExternalId))
            return HttpErrors.User.BrokenExternalId(userExternalIdStr);

        var currentUserContext = context.HttpContext.GetUserContext();

        if (currentUserContext.ExternalId == userExternalId)
            return HttpErrors.User.CannotModifyOwnUser(userExternalId);

        var userCache = context
            .HttpContext
            .RequestServices
            .GetRequiredService<UserCache>();

        var targetUser = await userCache.TryGetUser(
            userExternalId: userExternalId,
            cancellationToken: context.HttpContext.RequestAborted);

        if (targetUser is null)
            return HttpErrors.User.NotFound(userExternalId);

        if (targetUser.Roles.IsAdmin && !currentUserContext.Roles.IsAppOwner)
            return HttpErrors.User.CannotModifyAdminUser(userExternalId);

        return await next(context);
    }
}