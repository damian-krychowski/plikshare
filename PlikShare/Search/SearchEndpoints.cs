using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
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
        CancellationToken cancellationToken)
    {
        var response = getSearchQuery.Execute(
            user: await httpContext.GetUserContext(),
            workspaceExternalIds: request.WorkspaceExternalIds,
            boxExternalIds: request.BoxExternalIds,
            phrase: request.Phrase);

        return response;
    }
}