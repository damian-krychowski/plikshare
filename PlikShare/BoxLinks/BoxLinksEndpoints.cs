using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Boxes.Permissions;
using PlikShare.BoxLinks.Cache;
using PlikShare.BoxLinks.Delete;
using PlikShare.BoxLinks.Id;
using PlikShare.BoxLinks.RegenerateAccessCode;
using PlikShare.BoxLinks.RegenerateAccessCode.Contracts;
using PlikShare.BoxLinks.UpdateIsEnabled;
using PlikShare.BoxLinks.UpdateIsEnabled.Contracts;
using PlikShare.BoxLinks.UpdateName;
using PlikShare.BoxLinks.UpdateName.Contracts;
using PlikShare.BoxLinks.UpdatePermissions;
using PlikShare.BoxLinks.UpdatePermissions.Contracts;
using PlikShare.BoxLinks.UpdateWidgetOrigins;
using PlikShare.BoxLinks.UpdateWidgetOrigins.Contracts;
using PlikShare.BoxLinks.Validation;
using PlikShare.Core.Authorization;
using PlikShare.Core.Utils;
using PlikShare.Workspaces.Validation;

namespace PlikShare.BoxLinks;

public static class BoxLinksEndpoints
{
    public static void MapBoxLinksEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceExternalId}/box-links")
            .WithTags("BoxLinks")
            .RequireAuthorization(policyNames: AuthPolicy.Internal)
            .AddEndpointFilter<ValidateWorkspaceFilter>();

        group.MapPatch("/{boxLinkExternalId}/name", UpdateBoxLinkName)
            .WithName("UpdateBoxLinkName")
            .AddEndpointFilter<ValidateBoxLinkFilter>();

        group.MapPatch("/{boxLinkExternalId}/widget-origins", UpdateBoxLinkWidgetOrigins)
            .WithName("UpdateBoxLinkWidgetOrigins")
            .AddEndpointFilter<ValidateBoxLinkFilter>();

        group.MapPatch("/{boxLinkExternalId}/is-enabled", UpdateBoxLinkIsEnabled)
            .WithName("UpdateBoxLinkIsEnabled")
            .AddEndpointFilter<ValidateBoxLinkFilter>();

        group.MapPatch("/{boxLinkExternalId}/permissions", UpdateBoxLinkPermissions)
            .WithName("UpdateBoxLinkPermissions")
            .AddEndpointFilter<ValidateBoxLinkFilter>();

        group.MapPatch("/{boxLinkExternalId}/regenerate-access-code", RegenerateBoxLinkAccessCode)
            .WithName("RegenerateBoxLinkAccessCode")
            .AddEndpointFilter<ValidateBoxLinkFilter>();

        group.MapDelete("/{boxLinkExternalId}", DeleteBoxLink)
            .WithName("DeleteBoxLink")
            .AddEndpointFilter<ValidateBoxLinkFilter>();
    }
    
    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateBoxLinkWidgetOrigins(
        [FromRoute] BoxLinkExtId boxLinkExternalId,
        [FromBody] UpdateBoxLinkWidgetOriginsRequestDto request,
        HttpContext httpContext,
        UpdateBoxLinkWidgetOriginsQuery updateBoxLinkWidgetOriginsQuery,
        BoxLinkCache boxLinkCache,
        CancellationToken cancellationToken)
    {
        var resultCode = await updateBoxLinkWidgetOriginsQuery.Execute(
            boxLink: httpContext.GetBoxLinkContext(),
            widgetOrigins: request.WidgetOrigins,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateBoxLinkWidgetOriginsQuery.ResultCode.Ok:
                await boxLinkCache.InvalidateEntry(boxLinkExternalId, cancellationToken);
                return TypedResults.Ok();

            case UpdateBoxLinkWidgetOriginsQuery.ResultCode.BoxLinkNotFound:
                return HttpErrors.BoxLink.NotFound(boxLinkExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateBoxLinkWidgetOriginsQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> DeleteBoxLink(
        [FromRoute] BoxLinkExtId boxLinkExternalId,
        DeleteBoxLinkQuery deleteBoxLinkQuery,
        HttpContext httpContext,
        BoxLinkCache boxLinkCache,
        CancellationToken cancellationToken)
    {
        var resultCode = await deleteBoxLinkQuery.Execute(
            boxLink: httpContext.GetBoxLinkContext(),
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case DeleteBoxLinkQuery.ResultCode.Ok:
                await boxLinkCache.InvalidateEntry(boxLinkExternalId, cancellationToken);
                return TypedResults.Ok();

            case DeleteBoxLinkQuery.ResultCode.BoxLinkNotFound:
                return HttpErrors.BoxLink.NotFound(boxLinkExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(DeleteBoxLinkQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok<RegenerateBoxLinkAccessCodeResponseDto>, NotFound<HttpError>>> RegenerateBoxLinkAccessCode(
        [FromRoute] BoxLinkExtId boxLinkExternalId,
        RegenerateBoxLinkAccessCodeQuery regenerateBoxLinkAccessCodeQuery,
        HttpContext httpContext,
        BoxLinkCache boxLinkCache,
        CancellationToken cancellationToken)
    {
        var result = await regenerateBoxLinkAccessCodeQuery.Execute(
            boxLink: httpContext.GetBoxLinkContext(),
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case RegenerateBoxLinkAccessCodeQuery.ResultCode.Ok:
                await boxLinkCache.InvalidateEntry(boxLinkExternalId, cancellationToken);
                return TypedResults.Ok(new RegenerateBoxLinkAccessCodeResponseDto(
                    AccessCode: result.AccessCode!));

            case RegenerateBoxLinkAccessCodeQuery.ResultCode.BoxLinkNotFound:
                return HttpErrors.BoxLink.NotFound(boxLinkExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(RegenerateBoxLinkAccessCodeQuery),
                    resultValueStr: result.Code.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateBoxLinkPermissions(
        [FromRoute] BoxLinkExtId boxLinkExternalId,
        [FromBody] UpdateBoxLinkPermissionsRequestDto request,
        UpdateBoxLinkPermissionsQuery updateBoxLinkPermissionsQuery,
        HttpContext httpContext,
        BoxLinkCache boxLinkCache,
        CancellationToken cancellationToken)
    {
        var resultCode = await updateBoxLinkPermissionsQuery.Execute(
            boxLink: httpContext.GetBoxLinkContext(),
            permissions: new BoxPermissions(
                AllowDownload: request.AllowDownload,
                AllowUpload: request.AllowUpload,
                AllowList: request.AllowList,
                AllowDeleteFile: request.AllowDeleteFile,
                AllowRenameFile: request.AllowRenameFile,
                AllowMoveItems: request.AllowMoveItems,
                AllowCreateFolder: request.AllowCreateFolder,
                AllowDeleteFolder: request.AllowDeleteFolder,
                AllowRenameFolder: request.AllowRenameFolder),
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateBoxLinkPermissionsQuery.ResultCode.Ok:
                await boxLinkCache.InvalidateEntry(boxLinkExternalId, cancellationToken);
                return TypedResults.Ok();

            case UpdateBoxLinkPermissionsQuery.ResultCode.BoxLinkNotFound:
                return HttpErrors.BoxLink.NotFound(boxLinkExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateBoxLinkPermissionsQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateBoxLinkIsEnabled(
        [FromRoute] BoxLinkExtId boxLinkExternalId,
        [FromBody] UpdateBoxLinkIsEnabledRequestDto request,
        UpdateBoxLinkIsEnabledQuery updateBoxLinkIsEnabledQuery,
        HttpContext httpContext,
        BoxLinkCache boxLinkCache,
        CancellationToken cancellationToken)
    {
        var resultCode = await updateBoxLinkIsEnabledQuery.Execute(
            boxLink: httpContext.GetBoxLinkContext(),
            isEnabled: request.IsEnabled,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateBoxLinkIsEnabledQuery.ResultCode.Ok:
                await boxLinkCache.InvalidateEntry(boxLinkExternalId, cancellationToken);
                return TypedResults.Ok();

            case UpdateBoxLinkIsEnabledQuery.ResultCode.BoxLinkNotFound:
                return HttpErrors.BoxLink.NotFound(boxLinkExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateBoxLinkIsEnabledQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateBoxLinkName(
       [FromRoute] BoxLinkExtId boxLinkExternalId,
       [FromBody] UpdateBoxLinkNameRequestDto request,
       HttpContext httpContext,
       UpdateBoxLinkNameQuery updateBoxLinkNameQuery,
       BoxLinkCache boxLinkCache,
       CancellationToken cancellationToken)
    {
        var resultCode = await updateBoxLinkNameQuery.Execute(
            boxLink: httpContext.GetBoxLinkContext(),
            name: request.Name,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateBoxLinkNameQuery.ResultCode.Ok:
                await boxLinkCache.InvalidateEntry(boxLinkExternalId, cancellationToken);
                return TypedResults.Ok();

            case UpdateBoxLinkNameQuery.ResultCode.BoxLinkNotFound:
                return HttpErrors.BoxLink.NotFound(boxLinkExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateBoxLinkNameQuery),
                    resultValueStr: resultCode.ToString());
        }
    }
}