using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Agents.BoxAccess;
using PlikShare.Agents.Cache;
using PlikShare.Agents.Create;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Delete;
using PlikShare.Agents.Get;
using PlikShare.Agents.Get.Contracts;
using PlikShare.Agents.Id;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Operations.Details;
using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Operations.Id;
using PlikShare.Agents.Operations.List;
using PlikShare.Agents.Operations.List.Contracts;
using PlikShare.Agents.List;
using PlikShare.Agents.List.Contracts;
using PlikShare.Agents.ListWorkspaceBoxes;
using PlikShare.Agents.ListWorkspaceBoxes.Contracts;
using PlikShare.Agents.RotateToken;
using PlikShare.Agents.RotateToken.Contracts;
using PlikShare.Agents.Tools;
using PlikShare.Agents.Tools.Contracts;
using PlikShare.Agents.UpdateSettings;
using PlikShare.Agents.UpdateSettings.Contracts;
using PlikShare.Agents.WorkspaceAccess;
using PlikShare.AuditLog;
using PlikShare.Boxes.Id;
using PlikShare.Core.Authorization;
using PlikShare.Core.Utils;
using PlikShare.Users.Middleware;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;
#if DEBUG
using PlikShare.Agents.DevSeed;
#endif

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

        group.MapGet("/operations/pending", GetPendingOperations)
            .WithName("GetPendingAgentOperations");

        group.MapGet("/operations/{operationExternalId}/details", GetOperationDetails)
            .WithName("GetAgentOperationDetails");

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

        group.MapPatch("/{agentExternalId}/max-workspace-number", UpdateMaxWorkspaceNumber)
            .WithName("UpdateAgentMaxWorkspaceNumber");

        group.MapPatch("/{agentExternalId}/default-max-workspace-size", UpdateDefaultMaxWorkspaceSize)
            .WithName("UpdateAgentDefaultMaxWorkspaceSize");

        group.MapPatch("/{agentExternalId}/default-max-workspace-team-members", UpdateDefaultMaxWorkspaceTeamMembers)
            .WithName("UpdateAgentDefaultMaxWorkspaceTeamMembers");

        group.MapPatch("/{agentExternalId}/storage-access", UpdateStorageAccess)
            .WithName("UpdateAgentStorageAccess");

        group.MapGet("/{agentExternalId}/tools", GetAgentTools)
            .WithName("GetAgentTools");

        group.MapPatch("/{agentExternalId}/tools/{toolName}", UpdateAgentToolConfig)
            .WithName("UpdateAgentToolConfig");

        group.MapDelete("/{agentExternalId}/tools/{toolName}", ResetAgentToolConfig)
            .WithName("ResetAgentToolConfig");

        group.MapGet("/{agentExternalId}/workspaces/{workspaceExternalId}/tools", GetAgentWorkspaceTools)
            .WithName("GetAgentWorkspaceTools");

        group.MapPatch("/{agentExternalId}/workspaces/{workspaceExternalId}/tools/{toolName}", UpdateAgentWorkspaceToolOverride)
            .WithName("UpdateAgentWorkspaceToolOverride");

        group.MapDelete("/{agentExternalId}/workspaces/{workspaceExternalId}/tools/{toolName}", ResetAgentWorkspaceToolOverride)
            .WithName("ResetAgentWorkspaceToolOverride");

        group.MapGet("/workspaces/{workspaceExternalId}/boxes", ListWorkspaceBoxes)
            .WithName("ListAgentWorkspaceBoxes");

        group.MapPut("/{agentExternalId}/boxes/{boxExternalId}", GrantBoxAccess)
            .WithName("GrantAgentBoxAccess");

        group.MapDelete("/{agentExternalId}/boxes/{boxExternalId}", RevokeBoxAccess)
            .WithName("RevokeAgentBoxAccess");

        group.MapGet("/{agentExternalId}/boxes/{boxExternalId}/tools", GetAgentBoxTools)
            .WithName("GetAgentBoxTools");

        group.MapPatch("/{agentExternalId}/boxes/{boxExternalId}/tools/{toolName}", UpdateAgentBoxToolOverride)
            .WithName("UpdateAgentBoxToolOverride");

        group.MapDelete("/{agentExternalId}/boxes/{boxExternalId}/tools/{toolName}", ResetAgentBoxToolOverride)
            .WithName("ResetAgentBoxToolOverride");

        group.MapPost("/{agentExternalId}/operations/{operationExternalId}/approve", ApproveAgentOperation)
            .WithName("ApproveAgentOperation");

        group.MapPost("/{agentExternalId}/operations/{operationExternalId}/deny", DenyAgentOperation)
            .WithName("DenyAgentOperation");

#if DEBUG
        AgentDevSeedEndpoints.MapDevSeedEndpoints(group);
#endif
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
        AgentBoxAccessCache agentBoxAccessCache,
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

            await agentBoxAccessCache.InvalidateAllForAgent(
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

    private static async Task<Results<Ok<GetAgentToolsResponseDto>, NotFound<HttpError>>> GetAgentTools(
        [FromRoute] AgentExtId agentExternalId,
        GetAgentToolsQuery getAgentToolsQuery,
        CancellationToken cancellationToken)
    {
        var result = await getAgentToolsQuery.Execute(
            agentExternalId: agentExternalId,
            cancellationToken: cancellationToken);

        if (result is null)
            return HttpErrors.Agent.NotFound(agentExternalId);

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateAgentToolConfig(
        [FromRoute] AgentExtId agentExternalId,
        [FromRoute] string toolName,
        [FromBody] UpdateAgentToolConfigRequestDto request,
        AgentToolConfigQuery agentToolConfigQuery,
        AgentCache agentCache,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        if (AgentToolCatalog.TryGet(toolName) is null)
            return TypedResults.BadRequest(new HttpError
            {
                Code = "unknown-tool",
                Message = $"Unknown tool '{toolName}'."
            });

        var result = await agentToolConfigQuery.Upsert(
            agentExternalId: agentExternalId,
            toolName: toolName,
            isEnabled: request.IsEnabled,
            requiresApproval: request.RequiresApproval,
            cancellationToken: cancellationToken);

        if (result.Code == AgentToolConfigQuery.ResultCode.NotFound)
            return HttpErrors.Agent.NotFound(agentExternalId);

        await agentCache.InvalidateEntry(
            agentExternalId,
            cancellationToken);

        await auditLogService.Log(
            Audit.Agent.ToolConfigUpdatedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                agent: new Audit.AgentRef { ExternalId = agentExternalId, Name = result.AgentName! }),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> ResetAgentToolConfig(
        [FromRoute] AgentExtId agentExternalId,
        [FromRoute] string toolName,
        AgentToolConfigQuery agentToolConfigQuery,
        AgentCache agentCache,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        if (AgentToolCatalog.TryGet(toolName) is null)
            return TypedResults.BadRequest(new HttpError
            {
                Code = "unknown-tool",
                Message = $"Unknown tool '{toolName}'."
            });

        var result = await agentToolConfigQuery.Reset(
            agentExternalId: agentExternalId,
            toolName: toolName,
            cancellationToken: cancellationToken);

        if (result.Code == AgentToolConfigQuery.ResultCode.NotFound)
            return HttpErrors.Agent.NotFound(agentExternalId);

        await agentCache.InvalidateEntry(
            agentExternalId,
            cancellationToken);

        await auditLogService.Log(
            Audit.Agent.ToolConfigUpdatedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                agent: new Audit.AgentRef { ExternalId = agentExternalId, Name = result.AgentName! }),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok<GetAgentWorkspaceToolsResponseDto>, NotFound<HttpError>>> GetAgentWorkspaceTools(
        [FromRoute] AgentExtId agentExternalId,
        [FromRoute] WorkspaceExtId workspaceExternalId,
        GetAgentWorkspaceToolsQuery getAgentWorkspaceToolsQuery,
        CancellationToken cancellationToken)
    {
        var result = await getAgentWorkspaceToolsQuery.Execute(
            agentExternalId: agentExternalId,
            workspaceExternalId: workspaceExternalId,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GetAgentWorkspaceToolsQuery.ResultCode.Ok =>
                TypedResults.Ok(result.Response!),

            GetAgentWorkspaceToolsQuery.ResultCode.AgentNotFound =>
                HttpErrors.Agent.NotFound(agentExternalId),

            GetAgentWorkspaceToolsQuery.ResultCode.WorkspaceNotFound =>
                HttpErrors.Workspace.NotFound(workspaceExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(GetAgentWorkspaceToolsQuery),
                resultValueStr: result.Code.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateAgentWorkspaceToolOverride(
        [FromRoute] AgentExtId agentExternalId,
        [FromRoute] WorkspaceExtId workspaceExternalId,
        [FromRoute] string toolName,
        [FromBody] UpdateAgentWorkspaceToolOverrideRequestDto request,
        AgentToolWorkspaceOverrideQuery agentToolWorkspaceOverrideQuery,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var badRequest = ValidateWorkspaceTool(toolName);

        if (badRequest is not null)
            return badRequest;

        var result = await agentToolWorkspaceOverrideQuery.Upsert(
            agentExternalId: agentExternalId,
            workspaceExternalId: workspaceExternalId,
            toolName: toolName,
            isEnabled: request.IsEnabled,
            requiresApproval: request.RequiresApproval,
            cancellationToken: cancellationToken);

        return await HandleOverrideResult(
            result, agentExternalId, workspaceExternalId, httpContext, auditLogService, cancellationToken);
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> ResetAgentWorkspaceToolOverride(
        [FromRoute] AgentExtId agentExternalId,
        [FromRoute] WorkspaceExtId workspaceExternalId,
        [FromRoute] string toolName,
        AgentToolWorkspaceOverrideQuery agentToolWorkspaceOverrideQuery,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var badRequest = ValidateWorkspaceTool(toolName);

        if (badRequest is not null)
            return badRequest;

        var result = await agentToolWorkspaceOverrideQuery.Reset(
            agentExternalId: agentExternalId,
            workspaceExternalId: workspaceExternalId,
            toolName: toolName,
            cancellationToken: cancellationToken);

        return await HandleOverrideResult(
            result, agentExternalId, workspaceExternalId, httpContext, auditLogService, cancellationToken);
    }

    private static ListWorkspaceBoxesResponseDto ListWorkspaceBoxes(
        [FromRoute] WorkspaceExtId workspaceExternalId,
        ListWorkspaceBoxesQuery listWorkspaceBoxesQuery)
    {
        return listWorkspaceBoxesQuery.Execute(
            workspaceExternalId: workspaceExternalId);
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> GrantBoxAccess(
        [FromRoute] AgentExtId agentExternalId,
        [FromRoute] BoxExtId boxExternalId,
        AgentBoxAccessQuery agentBoxAccessQuery,
        AgentBoxAccessCache agentBoxAccessCache,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await agentBoxAccessQuery.Grant(
            agentExternalId: agentExternalId,
            boxExternalId: boxExternalId,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case AgentBoxAccessQuery.ResultCode.Ok:
                await agentBoxAccessCache.InvalidateEntry(
                    boxId: result.BoxId,
                    agentId: result.AgentId,
                    cancellationToken);

                await auditLogService.Log(
                    Audit.Agent.BoxAccessGrantedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        agent: new Audit.AgentRef { ExternalId = agentExternalId, Name = result.AgentName! },
                        box: new Audit.BoxRef { ExternalId = boxExternalId, Name = result.BoxName! }),
                    cancellationToken);

                return TypedResults.Ok();

            case AgentBoxAccessQuery.ResultCode.AgentNotFound:
                return HttpErrors.Agent.NotFound(agentExternalId);

            case AgentBoxAccessQuery.ResultCode.BoxNotFound:
                return HttpErrors.Box.NotFound(boxExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(AgentBoxAccessQuery),
                    resultValueStr: result.Code.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> RevokeBoxAccess(
        [FromRoute] AgentExtId agentExternalId,
        [FromRoute] BoxExtId boxExternalId,
        AgentBoxAccessQuery agentBoxAccessQuery,
        AgentBoxAccessCache agentBoxAccessCache,
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
                await agentBoxAccessCache.InvalidateEntry(
                    boxId: result.BoxId,
                    agentId: result.AgentId,
                    cancellationToken);

                await auditLogService.Log(
                    Audit.Agent.BoxAccessRevokedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        agent: new Audit.AgentRef { ExternalId = agentExternalId, Name = result.AgentName! },
                        box: new Audit.BoxRef { ExternalId = boxExternalId, Name = result.BoxName! }),
                    cancellationToken);

                return TypedResults.Ok();

            case AgentBoxAccessQuery.ResultCode.AgentNotFound:
                return HttpErrors.Agent.NotFound(agentExternalId);

            case AgentBoxAccessQuery.ResultCode.BoxNotFound:
                return HttpErrors.Box.NotFound(boxExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(AgentBoxAccessQuery),
                    resultValueStr: result.Code.ToString());
        }
    }

    private static async Task<Results<Ok<GetAgentBoxToolsResponseDto>, NotFound<HttpError>>> GetAgentBoxTools(
        [FromRoute] AgentExtId agentExternalId,
        [FromRoute] BoxExtId boxExternalId,
        GetAgentBoxToolsQuery getAgentBoxToolsQuery,
        CancellationToken cancellationToken)
    {
        var result = await getAgentBoxToolsQuery.Execute(
            agentExternalId: agentExternalId,
            boxExternalId: boxExternalId,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GetAgentBoxToolsQuery.ResultCode.Ok =>
                TypedResults.Ok(result.Response!),

            GetAgentBoxToolsQuery.ResultCode.AgentNotFound =>
                HttpErrors.Agent.NotFound(agentExternalId),

            GetAgentBoxToolsQuery.ResultCode.BoxNotFound =>
                HttpErrors.Box.NotFound(boxExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(GetAgentBoxToolsQuery),
                resultValueStr: result.Code.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateAgentBoxToolOverride(
        [FromRoute] AgentExtId agentExternalId,
        [FromRoute] BoxExtId boxExternalId,
        [FromRoute] string toolName,
        [FromBody] UpdateAgentBoxToolOverrideRequestDto request,
        AgentToolBoxOverrideQuery agentToolBoxOverrideQuery,
        CancellationToken cancellationToken)
    {
        var badRequest = ValidateBoxTool(toolName);

        if (badRequest is not null)
            return badRequest;

        var result = await agentToolBoxOverrideQuery.Upsert(
            agentExternalId: agentExternalId,
            boxExternalId: boxExternalId,
            toolName: toolName,
            isEnabled: request.IsEnabled,
            requiresApproval: request.RequiresApproval,
            cancellationToken: cancellationToken);

        return HandleBoxOverrideResult(result, agentExternalId, boxExternalId);
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> ResetAgentBoxToolOverride(
        [FromRoute] AgentExtId agentExternalId,
        [FromRoute] BoxExtId boxExternalId,
        [FromRoute] string toolName,
        AgentToolBoxOverrideQuery agentToolBoxOverrideQuery,
        CancellationToken cancellationToken)
    {
        var badRequest = ValidateBoxTool(toolName);

        if (badRequest is not null)
            return badRequest;

        var result = await agentToolBoxOverrideQuery.Reset(
            agentExternalId: agentExternalId,
            boxExternalId: boxExternalId,
            toolName: toolName,
            cancellationToken: cancellationToken);

        return HandleBoxOverrideResult(result, agentExternalId, boxExternalId);
    }

    private static BadRequest<HttpError>? ValidateBoxTool(string toolName)
    {
        var definition = AgentToolCatalog.TryGet(toolName);

        if (definition is null || !definition.IsBoxOverridable)
            return TypedResults.BadRequest(new HttpError
            {
                Code = "not-a-box-tool",
                Message = $"'{toolName}' cannot be overridden per box."
            });

        return null;
    }

    private static Results<Ok, NotFound<HttpError>, BadRequest<HttpError>> HandleBoxOverrideResult(
        AgentToolBoxOverrideQuery.Result result,
        AgentExtId agentExternalId,
        BoxExtId boxExternalId)
    {
        return result.Code switch
        {
            AgentToolBoxOverrideQuery.ResultCode.Ok =>
                TypedResults.Ok(),

            AgentToolBoxOverrideQuery.ResultCode.AgentNotFound =>
                HttpErrors.Agent.NotFound(agentExternalId),

            AgentToolBoxOverrideQuery.ResultCode.BoxNotFound =>
                HttpErrors.Box.NotFound(boxExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(AgentToolBoxOverrideQuery),
                resultValueStr: result.Code.ToString())
        };
    }

    private static async Task<Results<Ok<AgentOperationDetails>, NotFound<HttpError>>> GetOperationDetails(
        [FromRoute] AgentOperationExtId operationExternalId,
        AgentOperationLedger operationLedger,
        AgentCache agentCache,
        AgentOperationDetailsResolver detailsResolver,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var operation = operationLedger.GetByExternalId(operationExternalId);

        if (operation is null)
            return HttpErrors.Agent.OperationNotFound(operationExternalId);

        var agent = await agentCache.TryGetAgent(operation.AgentId, cancellationToken);
        var user = await httpContext.GetUserContext();

        if (agent is null || agent.Owner.Id != user.Id)
            return HttpErrors.Agent.OperationNotFound(operationExternalId);

        var details = detailsResolver.Resolve(operation);

        return TypedResults.Ok(details);
    }

    private static async Task<Ok<GetPendingAgentOperationsResponseDto>> GetPendingOperations(
        GetPendingAgentOperationsQuery getPendingAgentOperationsQuery,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var user = await httpContext.GetUserContext();

        var response = getPendingAgentOperationsQuery.Execute(
            ownerUserId: user.Id);

        return TypedResults.Ok(response);
    }

    private static Task<Results<Ok, NotFound<HttpError>>> ApproveAgentOperation(
        [FromRoute] AgentExtId agentExternalId,
        [FromRoute] AgentOperationExtId operationExternalId,
        AgentCache agentCache,
        AgentOperationLedger operationLedger,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ResolveAgentOperation(
            AgentOperationStatuses.Approved,
            agentExternalId,
            operationExternalId,
            agentCache,
            operationLedger,
            httpContext,
            cancellationToken);

    private static Task<Results<Ok, NotFound<HttpError>>> DenyAgentOperation(
        [FromRoute] AgentExtId agentExternalId,
        [FromRoute] AgentOperationExtId operationExternalId,
        AgentCache agentCache,
        AgentOperationLedger operationLedger,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ResolveAgentOperation(
            AgentOperationStatuses.Denied,
            agentExternalId,
            operationExternalId,
            agentCache,
            operationLedger,
            httpContext,
            cancellationToken);

    private static async Task<Results<Ok, NotFound<HttpError>>> ResolveAgentOperation(
        string targetStatus,
        AgentExtId agentExternalId,
        AgentOperationExtId operationExternalId,
        AgentCache agentCache,
        AgentOperationLedger operationLedger,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var agent = await agentCache.TryGetAgent(agentExternalId, cancellationToken);

        if (agent is null)
            return HttpErrors.Agent.NotFound(agentExternalId);

        var operation = operationLedger.GetByExternalId(operationExternalId);

        if (operation is null || operation.AgentId != agent.Id)
            return HttpErrors.Agent.OperationNotFound(operationExternalId);

        var user = await httpContext.GetUserContext();

        var result = await operationLedger.Resolve(
            externalId: operationExternalId,
            targetStatus: targetStatus,
            resolvedByUserId: user.Id,
            cancellationToken: cancellationToken);

        if (result == AgentOperationLedger.ResolveResultCode.NotPending)
            return HttpErrors.Agent.OperationNotPending(operationExternalId);

        return TypedResults.Ok();
    }

    private static BadRequest<HttpError>? ValidateWorkspaceTool(string toolName)
    {
        var definition = AgentToolCatalog.TryGet(toolName);

        if (definition is null || !definition.IsWorkspaceOverridable)
            return TypedResults.BadRequest(new HttpError
            {
                Code = "not-a-workspace-tool",
                Message = $"'{toolName}' cannot be overridden per workspace."
            });

        return null;
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> HandleOverrideResult(
        AgentToolWorkspaceOverrideQuery.Result result,
        AgentExtId agentExternalId,
        WorkspaceExtId workspaceExternalId,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        switch (result.Code)
        {
            case AgentToolWorkspaceOverrideQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Agent.ToolWorkspaceOverrideUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        agent: new Audit.AgentRef { ExternalId = agentExternalId, Name = result.AgentName! },
                        workspace: new Audit.WorkspaceRef { ExternalId = workspaceExternalId, Name = result.WorkspaceName! }),
                    cancellationToken);

                return TypedResults.Ok();

            case AgentToolWorkspaceOverrideQuery.ResultCode.AgentNotFound:
                return HttpErrors.Agent.NotFound(agentExternalId);

            case AgentToolWorkspaceOverrideQuery.ResultCode.WorkspaceNotFound:
                return HttpErrors.Workspace.NotFound(workspaceExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(AgentToolWorkspaceOverrideQuery),
                    resultValueStr: result.Code.ToString());
        }
    }
}
