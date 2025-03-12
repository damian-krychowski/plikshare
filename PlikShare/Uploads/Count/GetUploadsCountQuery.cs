using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Uploads.Count;

public class GetUploadsCountQuery(PlikShareDb plikShareDb)
{
    public SQLiteOneRowCommandResult<int> Execute(
        WorkspaceContext workspace,
        UserContext user)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .OneRowCmd(
                sql: """
                     SELECT COUNT(*)
                     FROM fu_file_uploads
                     WHERE 
                     	fu_workspace_id = $workspaceId
                     	AND fu_owner_identity_type = 'user_external_id'
                     	AND fu_owner_identity = $userExternalId
                        AND fu_is_completed = FALSE
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$userExternalId", user.ExternalId.Value)
            .Execute();
    }
}