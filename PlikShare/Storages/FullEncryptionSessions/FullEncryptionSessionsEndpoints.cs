using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.FullEncryptionSessions;

public static class FullEncryptionSessionsEndpoints
{
    public static void MapFullEncryptionSessionsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/full-encryption-sessions")
            .WithTags("Full Encryption Sessions")
            .RequireAuthorization(policyNames: AuthPolicy.Internal);

        group.MapGet("/", GetUnlockedSessions)
            .WithName("GetFullEncryptionSessions");

        group.MapDelete("/", LockAllSessions)
            .WithName("LockAllFullEncryptionSessions");

        group.MapDelete("/{storageExternalId}", LockSession)
            .WithName("LockFullEncryptionSession");
    }

    private static Ok<GetFullEncryptionSessionsResponseDto> GetUnlockedSessions(
        HttpContext httpContext,
        StorageClientStore storageClientStore)
    {
        var items = new List<FullEncryptionSessionItemDto>();

        foreach (var cookie in httpContext.Request.Cookies)
        {
            if (!cookie.Key.StartsWith(FullEncryptionSessionCookie.NamePrefix))
                continue;

            var storageExternalIdValue = cookie.Key.Substring(FullEncryptionSessionCookie.NamePrefix.Length);

            if (!StorageExtId.TryParse(storageExternalIdValue, null, out var storageExternalId))
                continue;

            var client = storageClientStore.TryGetClient(storageExternalId);

            if (client is null)
                continue;

            items.Add(new FullEncryptionSessionItemDto(
                StorageExternalId: storageExternalId,
                StorageName: client.Name));
        }

        return TypedResults.Ok(new GetFullEncryptionSessionsResponseDto(Items: items));
    }

    private static Ok LockSession(
        [FromRoute] StorageExtId storageExternalId,
        HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(
            FullEncryptionSessionCookie.GetCookieName(storageExternalId));

        return TypedResults.Ok();
    }

    private static Ok LockAllSessions(HttpContext httpContext)
    {
        foreach (var cookie in httpContext.Request.Cookies)
        {
            if (cookie.Key.StartsWith(FullEncryptionSessionCookie.NamePrefix))
            {
                httpContext.Response.Cookies.Delete(cookie.Key);
            }
        }

        return TypedResults.Ok();
    }
}

public record GetFullEncryptionSessionsResponseDto(
    List<FullEncryptionSessionItemDto> Items);

public record FullEncryptionSessionItemDto(
    StorageExtId StorageExternalId,
    string StorageName);
