using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.AuditLog.Policy.Contracts;
using PlikShare.AuditLog.Policy.Queries;
using PlikShare.Core.Authorization;
using PlikShare.Core.Utils;
using PlikShare.GeneralSettings;
using PlikShare.Workspaces.Id;

namespace PlikShare.AuditLog.Policy;

/// <summary>
/// Endpoints for reading and editing audit-log policies. All sit behind app-owner OR admin
/// with <see cref="Permissions.ManageAuditLog"/>. Three policy layers are exposed:
/// <list type="bullet">
///   <item><c>/app</c> — controls application-scoped events (auth, user mgmt, settings, …)</item>
///   <item><c>/workspace-defaults</c> — template for new workspaces (no retroactive effect)</item>
///   <item><c>/workspaces/{externalId}</c> — live policy for one specific workspace</item>
/// </list>
/// Plus a static <c>/catalog</c> for the editor UI and a <c>/volume-stats</c> aggregate.
/// </summary>
public static class AuditLogPolicyEndpoints
{
    public static void MapAuditLogPolicyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/audit-log/policy")
            .WithTags("AuditLog")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Policy = AuthPolicy.Internal,
                Roles = Roles.Admin
            })
            .AddEndpointFilter(new RequireAdminPermissionEndpointFilter(Permissions.ManageAuditLog));

        group.MapGet("/catalog", GetCatalog)
            .WithName("GetAuditLogPolicyCatalog");

        group.MapGet("/volume-stats", GetVolumeStats)
            .WithName("GetAuditLogPolicyVolumeStats");

        group.MapGet("/app", GetAppPolicy)
            .WithName("GetAuditLogAppPolicy");

        group.MapPut("/app", SetAppPolicy)
            .WithName("SetAuditLogAppPolicy");

        group.MapGet("/workspace-defaults", GetWorkspaceDefaultPolicy)
            .WithName("GetAuditLogWorkspaceDefaultPolicy");

        group.MapPut("/workspace-defaults", SetWorkspaceDefaultPolicy)
            .WithName("SetAuditLogWorkspaceDefaultPolicy");

        group.MapGet("/workspaces", ListWorkspaces)
            .WithName("ListAuditLogPolicyWorkspaces");

        group.MapGet("/workspaces/{externalId}", GetWorkspacePolicy)
            .WithName("GetAuditLogWorkspacePolicy");

        group.MapPut("/workspaces/{externalId}", SetWorkspacePolicy)
            .WithName("SetAuditLogWorkspacePolicy");
    }

    private static AuditLogPolicyWorkspacesDto ListWorkspaces(
        GetWorkspacesWithAuditLogPolicyQuery query)
    {
        var result = query.Execute();

        return new AuditLogPolicyWorkspacesDto
        {
            Workspaces = result.Workspaces
                .Select(w => new AuditLogPolicyWorkspaceItemDto
                {
                    ExternalId = w.ExternalId.Value,
                    Name = w.Name,
                    OwnerExternalId = w.OwnerExternalId.Value,
                    OwnerEmail = w.OwnerEmail,
                    DisabledCount = w.DisabledCount,
                    SeverityOverrideCount = w.SeverityOverrideCount
                })
                .ToList()
        };
    }

    private static AuditLogEventCatalogDto GetCatalog()
    {
        return new AuditLogEventCatalogDto
        {
            Events = AuditLogEventCatalog.All
                .Select(e => new AuditLogEventCatalogEntryDto
                {
                    EventType = e.EventType,
                    Category = e.Category,
                    Severity = e.Severity,
                    Description = e.Description,
                    Scope = e.Scope
                })
                .ToList()
        };
    }

    private static AuditLogVolumeStatsDto GetVolumeStats(
        [FromQuery] string? workspaceExternalId,
        [FromQuery] int? days,
        GetAuditLogVolumeStatsQuery query)
    {
        return query.Execute(
            workspaceExternalId: workspaceExternalId,
            daysWindow: days ?? 30);
    }

    private static AuditLogPolicyDto GetAppPolicy(AppSettings appSettings)
    {
        return ToDto(appSettings.AuditLogAppPolicy);
    }

    private static Results<Ok, BadRequest<HttpError>> SetAppPolicy(
        [FromBody] AuditLogPolicyDto request,
        AppSettings appSettings)
    {
        if (ValidateSeverities(request) is { } badRequest)
            return badRequest;

        appSettings.SetAuditLogAppPolicy(FromDto(request));
        return TypedResults.Ok();
    }

    private static AuditLogPolicyDto GetWorkspaceDefaultPolicy(AppSettings appSettings)
    {
        return ToDto(appSettings.AuditLogWorkspaceDefaultPolicy);
    }

    private static Results<Ok, BadRequest<HttpError>> SetWorkspaceDefaultPolicy(
        [FromBody] AuditLogPolicyDto request,
        AppSettings appSettings)
    {
        if (ValidateSeverities(request) is { } badRequest)
            return badRequest;

        appSettings.SetAuditLogWorkspaceDefaultPolicy(FromDto(request));
        return TypedResults.Ok();
    }

    private static Results<Ok<GetWorkspacePolicyResponseDto>, NotFound<HttpError>> GetWorkspacePolicy(
        [FromRoute] WorkspaceExtId externalId,
        GetWorkspaceAuditLogPolicyQuery query)
    {
        var result = query.Execute(externalId);

        if (!result.WorkspaceFound)
            return HttpErrors.Workspace.NotFound(externalId);

        return TypedResults.Ok(new GetWorkspacePolicyResponseDto
        {
            WorkspaceExternalId = externalId.Value,
            WorkspaceName = result.WorkspaceName,
            DisabledEventTypes = result.Policy.DisabledEventTypes.OrderBy(s => s).ToList(),
            SeverityOverrides = result.Policy.SeverityOverrides.Count == 0
                ? null
                : result.Policy.SeverityOverrides.ToDictionary(kv => kv.Key, kv => kv.Value)
        });
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> SetWorkspacePolicy(
        [FromRoute] WorkspaceExtId externalId,
        [FromBody] AuditLogPolicyDto request,
        UpdateWorkspaceAuditLogPolicyQuery query,
        CancellationToken cancellationToken)
    {
        if (ValidateSeverities(request) is { } badRequest)
            return badRequest;

        var result = await query.Execute(
            workspaceExternalId: externalId,
            policy: FromDto(request),
            cancellationToken: cancellationToken);

        return result == UpdateWorkspaceAuditLogPolicyQuery.ResultCode.WorkspaceNotFound
            ? HttpErrors.Workspace.NotFound(externalId)
            : TypedResults.Ok();
    }

    /// <summary>
    /// Reject the PUT when the client sent a severity value outside <see cref="AuditLogSeverities"/>.
    /// Unknown event-type keys are tolerated (they're sparse and forward-compatible — the eval just
    /// ignores them); the same tolerance for severity values would be a footgun, since a typo'd
    /// "warn" would silently degrade to "no override" instead of warning the admin.
    /// </summary>
    private static BadRequest<HttpError>? ValidateSeverities(AuditLogPolicyDto dto)
    {
        if (dto.SeverityOverrides is null)
            return null;

        foreach (var kv in dto.SeverityOverrides)
        {
            if (!AuditLogSeverities.IsValid(kv.Value))
            {
                return TypedResults.BadRequest(new HttpError
                {
                    Code = "invalid-severity",
                    Message = $"Severity '{kv.Value}' for event '{kv.Key}' is not one of: " +
                              $"{string.Join(", ", AuditLogSeverities.All)}."
                });
            }
        }

        return null;
    }

    private static AuditLogPolicyDto ToDto(AuditLogPolicy policy) => new()
    {
        DisabledEventTypes = policy.DisabledEventTypes.OrderBy(s => s).ToList(),
        SeverityOverrides = policy.SeverityOverrides.Count == 0
            ? null
            : policy.SeverityOverrides.ToDictionary(kv => kv.Key, kv => kv.Value)
    };

    private static AuditLogPolicy FromDto(AuditLogPolicyDto dto) =>
        new(
            disabledEventTypes: dto.DisabledEventTypes,
            severityOverrides: dto.SeverityOverrides);
}
