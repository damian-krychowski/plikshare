using Microsoft.AspNetCore.Mvc;
using PlikShare.Antiforgery;
using PlikShare.Core.Authorization;
using PlikShare.Locks.CheckFileLocks;
using PlikShare.Locks.CheckFileLocks.Contracts;

namespace PlikShare.Locks;

public static class LockStatusEndpoints
{
    public static void MapLockStatusEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/lock-status")
            .WithTags("Lock Status")
            .RequireAuthorization(policyNames: AuthPolicy.InternalOrBoxLink);

        group.MapPost("/files", CheckFileLocks)
            .WithName("CheckFileLocks")
            .WithMetadata(new DisableAutoAntiforgeryCheck());
    }

    private static CheckFileLocksResponseDto CheckFileLocks(
        [FromBody] CheckFileLocksRequestDto request,
        CheckFileLocksQuery checkFileLocksQuery)
    {
        var response = checkFileLocksQuery.Execute(
            request.ExternalIds);

        return response;
    }
}