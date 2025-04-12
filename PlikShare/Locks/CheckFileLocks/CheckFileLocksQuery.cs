using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Locks.CheckFileLocks.Contracts;

namespace PlikShare.Locks.CheckFileLocks;

public class CheckFileLocksQuery(PlikShareDb plikShareDb)
{
    public CheckFileLocksResponseDto Execute(
        List<string> externalIds)
    {
        var fileExternalIds = new HashSet<string>(
            externalIds);

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