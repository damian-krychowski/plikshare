using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Agents.BoxAccess;
using PlikShare.Agents.BoxAccess.Contracts;
using PlikShare.Agents.Cache;
using PlikShare.Agents.Create;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Delete;
using PlikShare.Agents.Get;
using PlikShare.Agents.Get.Contracts;
using PlikShare.Agents.Id;
using PlikShare.Agents.List;
using PlikShare.Agents.List.Contracts;
using PlikShare.Agents.ListWorkspaceBoxes;
using PlikShare.Agents.ListWorkspaceBoxes.Contracts;
using PlikShare.Agents.RotateToken;
using PlikShare.Agents.RotateToken.Contracts;
using PlikShare.Agents.UpdateSettings;
using PlikShare.Agents.UpdateSettings.Contracts;
using PlikShare.Agents.WorkspaceAccess;
using PlikShare.AuditLog;
using PlikShare.Boxes.Id;
using PlikShare.Boxes.Permissions;
using PlikShare.Core.Authorization;
using PlikShare.Core.Utils;
using PlikShare.Users.Middleware;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Agents;

public static class AgentsEndpoints
{
    public static void MapAgentsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/agents")
            .WithTags("Agents")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Policy = AuthPolicy.Internal,
                Roles = $"{Roles.Admin}"
            })
            .AddEndpointFilter(new RequireAdminPermissionEndpointFilter(Permissions.ManageAgents));

        group.MapGet("/", GetAgents)
            .WithName("GetAgents");

        group.MapGet("/{agentExternalId}", GetAgentDetailsHandler)
            .WithName("GetAgentDetails");

        group.MapGet("/workspaces/{workspaceExternalId}/boxes", ListWorkspaceBoxes)
            .WithName("ListAgentWorkspaceBoxes");

        group.MapPost("/", CreateAgent)
            .WithName("CreateAgent");

        group.MapDelete("/{agentExternalId}", DeleteAgent)
            .WithName("DeleteAgent");

        group.MapPost("/{agentExternalId}/token/rotate", RotateToken)
            .WithName("RotateAgentToken");

        group.MapPut("/{agentExternalId}/workspaces/{workspaceExternalId}", GrantWorkspaceAccess)
            .WithName("GrantAgentWorkspaceAccess");

        group.MapDelete("/{agentExternalId}/workspaces/{workspaceExternalId}", RevokeWorkspaceAccess)
            .WithName("RevokeAgentWorkspaceAccess");

        group.MapPut("/{agentExternalId}/boxes/{boxExternalId}", GrantBoxAccess)
            .WithName("GrantAgentBoxAccess");

        group.MapDelete("/{agentExternalId}/boxes/{boxExternalId}", RevokeBoxAccess)
            .WithName("RevokeAgentBoxAccess");

        group.MapPatch("/{agentExternalId}/permissions-and-roles", UpdatePermissionsAndRoles)
            .WithName("UpdateAgentPermissionsAndRoles");

        group.MapPatch("/{agentExternalId}/max-workspace-number", UpdateMaxWorkspaceNumber)
            .WithName("UpdateAgentMaxWorkspaceNumber");

        group.MapPatch("/{agentExternalId}/default-max-workspace-size", UpdateDefaultMaxWorkspaceSize)
            .WithName("UpdateAgentDefaultMaxWorkspaceSize");

        group.MapPatch("/{agentExternalId}/default-max-workspace-team-members", UpdateDefaultMaxWorkspaceTeamMembers)
            .WithName("UpdateAgentDefaultMaxWorkspaceTeamMembers");

        group.MapPatch("/{agentExternalId}/storage-access", UpdateStorageAccess)
            .WithName("UpdateAgentStorageAccess");
    }

    private static GetAgentsResponseDto GetAgents(
        GetAgentsQuery getAgentsQuery)
    {
        return getAgentsQuery.Execute();
    }

    private static Results<Ok<GetAgentDetails.ResponseDto>, NotFound<HttpError>> GetAgentDetailsHandler(
        [FromRoute] AgentExtId agentExternalId,
        GetAgentDetailsQuery getAgentDetailsQuery)
    {
        var result = getAgentDetailsQuery.Execute(
            externalId: agentExternalId);

        if (result is null)
            return HttpErrors.Agent.NotFound(agentExternalId);

        return TypedResults.Ok(result);
    }

    private static ListWorkspaceBoxesResponseDto ListWorkspaceBoxes(
        [FromRoute] WorkspaceExtId workspaceExternalId,
        ListWorkspaceBoxesQuery listWorkspaceBoxesQuery)
    {
        return listWorkspaceBoxesQuery.Execute(
            workspaceExternalId: workspaceExternalId);
    }

    private static async Task<Ok<CreateAgentResponseDto>> CreateAgent(
        [FromBody] CreateAgentRequestDto request,
        CreateAgentQuery createAgentQuery,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var owner = await httpContext.GetUserContext();

        var result = await createAgentQuery.Execute(
            name: request.Name,
            ownerUserId: owner.Id,
            cancellationToken: cancellationToken);

        await auditLogService.Log(
            Audit.Agent.CreatedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                agent: new Audit.AgentRef
                {
                    ExternalId = result.ExternalId,
                    Name = request.Name
                }),
            cancellationToken);

        return TypedResults.Ok(new CreateAgentResponseDto
        {
            ExternalId = result.ExternalId,
            Token = result.Token!,
            TokenMasked = result.TokenMasked!
        });
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> DeleteAgent(
        [FromRoute] AgentExtId agentExternalId,
        DeleteAgentQuery deleteAgentQuery,
        AgentCache agentCache,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await deleteAgentQuery.Execute(
            externalId: agentExternalId,
            cancellationToken: cancellationToken);

        if (result.Code == DeleteAgentQuery.ResultCode.Ok)
        {
            await agentCache.InvalidateEntry(
                result.Id,
                cancellationToken);

            await workspaceAgentMembershipCache.InvalidateAllForAgent(
                result.Id,
                cancellationToken);

            await auditLogService.Log(
                Audit.Agent.DeletedEntry(
                    actor: httpContext.GetAuditLogActorContext(),
                    agent: new Audit.AgentRef
                    {
                        ExternalId = agentExternalId,
                        Name = result.Name!
                    }),
                cancellationToken);
        }

        return result.Code switch
        {
            DeleteAgentQuery.ResultCode.Ok =>
                TypedResults.Ok(),

            DeleteAgentQuery.ResultCode.NotFound =>
                HttpErrors.Agent.NotFound(agentExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(DeleteAgentQuery),
                resultValueStr: result.ToString())
        };
    }

    private static async Task<Results<Ok<RotateAgentTokenResponseDto>, NotFound<HttpError>>> RotateToken(
        [FromRoute] AgentExtId agentExternalId,
        RotateAgentTokenQuery rotateAgentTokenQuery,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await rotateAgentTokenQuery.Execute(
            agentExternalId: agentExternalId,
            cancellationToken: cancellationToken);

        if (result.Code == RotateAgentTokenQuery.ResultCode.NotFound)
            return HttpErrors.Agent.NotFound(agentExternalId);

        await auditLogService.Log(
            Audit.Agent.TokenRotatedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                agent: new Audit.AgentRef
                {
                    ExternalId = agentExternalId,
                    Name = result.AgentName!
                }),
            cancellationToken);

        return TypedResults.Ok(new RotateAgentTokenResponseDto
        {
            Token = result.Token!,
            TokenMasked = result.TokenMasked!
        });
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> GrantWorkspaceAccess(
        [FromRoute] AgentExtId agentExternalId,
        [FromRoute] WorkspaceExtId workspaceExternalId,
        AgentWorkspaceAccessQuery agentWorkspaceAccessQuery,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await agentWorkspaceAccessQuery.Grant(
            agentExternalId: agentExternalId,
            workspaceExternalId: workspaceExternalId,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case AgentWorkspaceAccessQuery.ResultCode.Ok:
                await workspaceAgentMembershipCache.InvalidateEntry(
                    workspaceId: result.WorkspaceId,
                    agentId: result.AgentId,
                    cancellationToken);

                await auditLogService.Log(
                    Audit.Agent.WorkspaceAccessGrantedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        agent: new Audit.AgentRef
                        {
                            ExternalId = agentExternalId,
                            Name = result.AgentName!
                        },
                        workspace: new Audit.WorkspaceRef
                        {
                            ExternalId = workspaceExternalId,
                            Name = result.WorkspaceName!
                        }),
                    cancellationToken);

                return TypedResults.Ok();

            case AgentWorkspaceAccessQuery.ResultCode.AgentNotFound:
                return HttpErrors.Agent.NotFound(agentExternalId);

            case AgentWorkspaceAccessQuery.ResultCode.WorkspaceNotFound:
                return HttpErrors.Workspace.NotFound(workspaceExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(AgentWorkspaceAccessQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> RevokeWorkspaceAccess(
        [FromRoute] AgentExtId agentExternalId,
        [FromRoute] WorkspaceExtId workspaceExternalId,
        AgentWorkspaceAccessQuery agentWorkspaceAccessQuery,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await agentWorkspaceAccessQuery.Revoke(
            agentExternalId: agentExternalId,
            workspaceExternalId: workspaceExternalId,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case AgentWorkspaceAccessQuery.ResultCode.Ok:
                await workspaceAgentMembershipCache.InvalidateEntry(
                    workspaceId: result.WorkspaceId,
                    agentId: result.AgentId,
                    cancellationToken);

                await auditLogService.Log(
                    Audit.Agent.WorkspaceAccessRevokedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        agent: new Audit.AgentRef
                        {
                            ExternalId = agentExternalId,
                            Name = result.AgentName!
                        },
                        workspace: new Audit.WorkspaceRef
                        {
                            ExternalId = workspaceExternalId,
                            Name = result.WorkspaceName!
                        }),
                    cancellationToken);

                return TypedResults.Ok();

            case AgentWorkspaceAccessQuery.ResultCode.AgentNotFound:
                return HttpErrors.Agent.NotFound(agentExternalId);

            case AgentWorkspaceAccessQuery.ResultCode.WorkspaceNotFound:
                return HttpErrors.Workspace.NotFound(workspaceExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(AgentWorkspaceAccessQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> GrantBoxAccess(
        [FromRoute] AgentExtId agentExternalId,
        [FromRoute] BoxExtId boxExternalId,
        [FromBody] GrantAgentBoxAccessRequestDto request,
        AgentBoxAccessQuery agentBoxAccessQuery,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await agentBoxAccessQuery.Grant(
            agentExternalId: agentExternalId,
            boxExternalId: boxExternalId,
            permissions: new BoxPermissions(
                AllowDownload: request.AllowDownload,
                AllowUpload: request.AllowUpload,
                AllowList: request.AllowList,
                AllowDeleteFile: request.AllowDeleteFile,
                AllowRenameFile: request.AllowRenameFile,
                AllowMoveItems: request.AllowMoveItems,
                AllowCreateFolder: request.AllowCreateFolder,
                AllowRenameFolder: request.AllowRenameFolder,
                AllowDeleteFolder: request.AllowDeleteFolder),
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case AgentBoxAccessQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Agent.BoxAccessGrantedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        agent: new Audit.AgentRef
                        {
                            ExternalId = agentExternalId,
                            Name = result.AgentName!
                        },
                        box: new Audit.BoxRef
                        {
                            ExternalId = boxExternalId,
                            Name = result.BoxName!
                        }),
                    cancellationToken);

                return TypedResults.Ok();

            case AgentBoxAccessQuery.ResultCode.AgentNotFound:
                return HttpErrors.Agent.NotFound(agentExternalId);

            case AgentBoxAccessQuery.ResultCode.BoxNotFound:
                return HttpErrors.Box.NotFound(boxExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(AgentBoxAccessQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> RevokeBoxAccess(
        [FromRoute] AgentExtId agentExternalId,
        [FromRoute] BoxExtId boxExternalId,
        AgentBoxAccessQuery agentBoxAccessQuery,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await agentBoxAccessQuery.Revoke(
            agentExternalId: agentExternalId,
            boxExternalId: boxExternalId,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case AgentBoxAccessQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Agent.BoxAccessRevokedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        agent: new Audit.AgentRef
                        {
                            ExternalId = agentExternalId,
                            Name = result.AgentName!
                        },
                        box: new Audit.BoxRef
                        {
                            ExternalId = boxExternalId,
                            Name = result.BoxName!
                        }),
                    cancellationToken);

                return TypedResults.Ok();

            case AgentBoxAccessQuery.ResultCode.AgentNotFound:
                return HttpErrors.Agent.NotFound(agentExternalId);

            case AgentBoxAccessQuery.ResultCode.BoxNotFound:
                return HttpErrors.Box.NotFound(boxExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(AgentBoxAccessQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdatePermissionsAndRoles(
        [FromRoute] AgentExtId agentExternalId,
        [FromBody] UpdateAgentPermissionsAndRolesRequestDto request,
        UpdateAgentSettingsQuery updateAgentSettingsQuery,
        AgentCache agentCache,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await updateAgentSettingsQuery.UpdatePermissionsAndRoles(
            agentExternalId: agentExternalId,
            request: request,
            cancellationToken: cancellationToken);

        if (result.Code == UpdateAgentSettingsQuery.ResultCode.NotFound)
            return HttpErrors.Agent.NotFound(agentExternalId);

        await agentCache.InvalidateEntry(
            agentExternalId,
            cancellationToken);

        await auditLogService.Log(
            Audit.Agent.PermissionsAndRolesUpdatedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                agent: new Audit.AgentRef { ExternalId = agentExternalId, Name = result.AgentName! }),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateMaxWorkspaceNumber(
        [FromRoute] AgentExtId agentExternalId,
        [FromBody] UpdateAgentMaxWorkspaceNumberRequestDto request,
        UpdateAgentSettingsQuery updateAgentSettingsQuery,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await updateAgentSettingsQuery.UpdateMaxWorkspaceNumber(
            agentExternalId: agentExternalId,
            maxWorkspaceNumber: request.MaxWorkspaceNumber,
            cancellationToken: cancellationToken);

        if (result.Code == UpdateAgentSettingsQuery.ResultCode.NotFound)
            return HttpErrors.Agent.NotFound(agentExternalId);

        await auditLogService.Log(
            Audit.Agent.MaxWorkspaceNumberUpdatedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                agent: new Audit.AgentRef { ExternalId = agentExternalId, Name = result.AgentName! }),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateDefaultMaxWorkspaceSize(
        [FromRoute] AgentExtId agentExternalId,
        [FromBody] UpdateAgentDefaultMaxWorkspaceSizeRequestDto request,
        UpdateAgentSettingsQuery updateAgentSettingsQuery,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await updateAgentSettingsQuery.UpdateDefaultMaxWorkspaceSize(
            agentExternalId: agentExternalId,
            maxSizeInBytes: request.MaxSizeInBytes,
            cancellationToken: cancellationToken);

        if (result.Code == UpdateAgentSettingsQuery.ResultCode.NotFound)
            return HttpErrors.Agent.NotFound(agentExternalId);

        await auditLogService.Log(
            Audit.Agent.DefaultMaxWorkspaceSizeUpdatedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                agent: new Audit.AgentRef { ExternalId = agentExternalId, Name = result.AgentName! }),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateDefaultMaxWorkspaceTeamMembers(
        [FromRoute] AgentExtId agentExternalId,
        [FromBody] UpdateAgentDefaultMaxWorkspaceTeamMembersRequestDto request,
        UpdateAgentSettingsQuery updateAgentSettingsQuery,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await updateAgentSettingsQuery.UpdateDefaultMaxWorkspaceTeamMembers(
            agentExternalId: agentExternalId,
            maxTeamMembers: request.MaxTeamMembers,
            cancellationToken: cancellationToken);

        if (result.Code == UpdateAgentSettingsQuery.ResultCode.NotFound)
            return HttpErrors.Agent.NotFound(agentExternalId);

        await auditLogService.Log(
            Audit.Agent.DefaultMaxWorkspaceTeamMembersUpdatedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                agent: new Audit.AgentRef { ExternalId = agentExternalId, Name = result.AgentName! }),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateStorageAccess(
        [FromRoute] AgentExtId agentExternalId,
        [FromBody] UpdateAgentStorageAccessRequestDto request,
        UpdateAgentSettingsQuery updateAgentSettingsQuery,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await updateAgentSettingsQuery.UpdateStorageAccess(
            agentExternalId: agentExternalId,
            mode: request.Mode,
            storageExternalIds: request.StorageExternalIds,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case UpdateAgentSettingsQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Agent.StorageAccessUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        agent: new Audit.AgentRef { ExternalId = agentExternalId, Name = result.AgentName! }),
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateAgentSettingsQuery.ResultCode.NotFound:
                return HttpErrors.Agent.NotFound(agentExternalId);

            case UpdateAgentSettingsQuery.ResultCode.UnknownStorageExternalIds:
                return TypedResults.BadRequest(new HttpError
                {
                    Code = "unknown-storage-external-ids",
                    Message = $"Unknown storage external ids: {string.Join(", ", result.UnknownExternalIds ?? [])}"
                });

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateAgentSettingsQuery),
                    resultValueStr: result.ToString());
        }
    }
}
