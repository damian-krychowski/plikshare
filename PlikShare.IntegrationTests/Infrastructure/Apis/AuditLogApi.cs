using Flurl.Http;
using PlikShare.AuditLog.Contracts;
using PlikShare.AuditLog.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class AuditLogApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<GetAuditLogResponseDto> GetLogs(
        GetAuditLogRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<GetAuditLogResponseDto, GetAuditLogRequestDto>(
            appUrl: appUrl,
            apiPath: "api/audit-log",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery,
            isResponseInProtobuf: true);
    }

    public async Task<GetAuditLogEntryDetailsResponseDto> GetEntryDetails(
        AuditLogExtId externalId,
        SessionAuthCookie? cookie,
        Cookie? userEncryptionSession = null)
    {
        return await flurlClient.ExecuteGet<GetAuditLogEntryDetailsResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/audit-log/{externalId.Value}",
            cookie: cookie,
            extraCookie: userEncryptionSession);
    }

    public async Task<AuditLogStatsResponseDto> GetStats(
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<AuditLogStatsResponseDto>(
            appUrl: appUrl,
            apiPath: "api/audit-log/stats",
            cookie: cookie);
    }

    public async Task<DeleteOldAuditLogsResponseDto> DeleteOldLogs(
        DeleteOldAuditLogsRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<DeleteOldAuditLogsResponseDto, DeleteOldAuditLogsRequestDto>(
            appUrl: appUrl,
            apiPath: "api/audit-log/delete-old",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<ArchiveAuditLogsResponseDto> ArchiveLogs(
        ArchiveAuditLogsRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<ArchiveAuditLogsResponseDto, ArchiveAuditLogsRequestDto>(
            appUrl: appUrl,
            apiPath: "api/audit-log/archive",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }
}
