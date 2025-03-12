using PlikShare.Core.Authorization;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
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
            .WithName("CheckFileLocks");
    }

    private static CheckFileLocksResponseDto CheckFileLocks(
        CheckFileLocksRequestDto request,
        PlikShareDb plikShareDb)
    {
        var fileExternalIds = new HashSet<string>(request.ExternalIds);

        using var connection = plikShareDb.OpenConnection();
        var result = connection
            .Cmd(
                sql: """
                     SELECT fi_external_id
                     FROM fi_files
                     WHERE fi_is_upload_completed = FALSE
                     """,
                readRowFunc: reader => reader.GetString(0))
            .Execute();

        var response = new CheckFileLocksResponseDto(
            LockedExternalIds: result
                .Where(fileExternalIds.Contains)
                .ToList());

        return response;
    }
}