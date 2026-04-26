using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Core.Encryption;
using PlikShare.Core.Protobuf;
using PlikShare.Search.Get;
using PlikShare.Search.Get.Contracts;
using PlikShare.Users.Middleware;

namespace PlikShare.Search;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/search")
            .WithTags("Search")
            .RequireAuthorization(policyNames: AuthPolicy.Internal);

        group.MapPost("/", Search)
            .WithName("Search")
            .WithProtobufResponse();
    }

    private static async ValueTask<SearchResponseDto> Search(
        [FromBody] SearchRequestDto request,
        HttpContext httpContext,
        GetSearchQuery getSearchQuery,
        SearchSessionLoader searchSessionLoader,
        CancellationToken cancellationToken)
    {
        var user = await httpContext.GetUserContext();

        using var privateKey = UserEncryptionSessionCookie.TryReadPrivateKey(
            httpContext,
            user.ExternalId);

        return await getSearchQuery.Execute(
            user: user,
            privateKey: privateKey,
            workspaceExternalIds: request.WorkspaceExternalIds,
            boxExternalIds: request.BoxExternalIds,
            phrase: request.Phrase,
            cancellationToken: cancellationToken);
    }
}
