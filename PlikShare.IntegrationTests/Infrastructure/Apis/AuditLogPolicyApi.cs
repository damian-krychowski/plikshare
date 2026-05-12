using Flurl.Http;
using PlikShare.AuditLog.Policy.Contracts;
using PlikShare.Workspaces.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

/// <summary>
/// Wrapper around the <c>/api/audit-log/policy</c> endpoint family. All endpoints require admin
/// + <c>Permissions.ManageAuditLog</c> (or app-owner). GET endpoints don't need an antiforgery
/// token; PUTs do.
/// </summary>
public class AuditLogPolicyApi(IFlurlClient flurlClient, string appUrl)
{
    public Task<AuditLogEventCatalogDto> GetCatalog(
        SessionAuthCookie? cookie)
    {
        return flurlClient.ExecuteGet<AuditLogEventCatalogDto>(
            appUrl: appUrl,
            apiPath: "api/audit-log/policy/catalog",
            cookie: cookie);
    }

    public Task<AuditLogVolumeStatsDto> GetVolumeStats(
        SessionAuthCookie? cookie,
        string? workspaceExternalId = null,
        int? days = null)
    {
        var query = new List<string>();
        if (workspaceExternalId is not null)
            query.Add($"workspaceExternalId={Uri.EscapeDataString(workspaceExternalId)}");
        if (days is not null)
            query.Add($"days={days}");

        var path = "api/audit-log/policy/volume-stats";
        if (query.Count > 0)
            path += "?" + string.Join("&", query);

        return flurlClient.ExecuteGet<AuditLogVolumeStatsDto>(
            appUrl: appUrl,
            apiPath: path,
            cookie: cookie);
    }

    public Task<AuditLogPolicyDto> GetAppPolicy(
        SessionAuthCookie? cookie)
    {
        return flurlClient.ExecuteGet<AuditLogPolicyDto>(
            appUrl: appUrl,
            apiPath: "api/audit-log/policy/app",
            cookie: cookie);
    }

    public Task SetAppPolicy(
        AuditLogPolicyDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return flurlClient.ExecutePut(
            appUrl: appUrl,
            apiPath: "api/audit-log/policy/app",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public Task<AuditLogPolicyDto> GetWorkspaceDefaultPolicy(
        SessionAuthCookie? cookie)
    {
        return flurlClient.ExecuteGet<AuditLogPolicyDto>(
            appUrl: appUrl,
            apiPath: "api/audit-log/policy/workspace-defaults",
            cookie: cookie);
    }

    public Task SetWorkspaceDefaultPolicy(
        AuditLogPolicyDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return flurlClient.ExecutePut(
            appUrl: appUrl,
            apiPath: "api/audit-log/policy/workspace-defaults",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public Task<GetWorkspacePolicyResponseDto> GetWorkspacePolicy(
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie)
    {
        return flurlClient.ExecuteGet<GetWorkspacePolicyResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/audit-log/policy/workspaces/{workspaceExternalId.Value}",
            cookie: cookie);
    }

    public Task SetWorkspacePolicy(
        WorkspaceExtId workspaceExternalId,
        AuditLogPolicyDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return flurlClient.ExecutePut(
            appUrl: appUrl,
            apiPath: $"api/audit-log/policy/workspaces/{workspaceExternalId.Value}",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public Task<AuditLogPolicyWorkspacesDto> ListWorkspaces(
        SessionAuthCookie? cookie)
    {
        return flurlClient.ExecuteGet<AuditLogPolicyWorkspacesDto>(
            appUrl: appUrl,
            apiPath: "api/audit-log/policy/workspaces",
            cookie: cookie);
    }
}
