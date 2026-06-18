using PlikShare.Agents.Operations;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Workspaces.Rename.Contracts;

namespace PlikShare.Mcp.Workspaces.Rename;

/// <summary>
/// Resolves a rename_workspace operation's stored id into the workspace's current name and the
/// requested new name, so a human reviewing the approval sees exactly what gets renamed and to what.
/// </summary>
public class RenameWorkspaceOperationDetailsResolver(
    PlikShareDb plikShareDb)
{
    public RenameWorkspaceOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<RenameWorkspaceParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        return new RenameWorkspaceOperationDetails
        {
            WorkspaceExternalId = parameters.WorkspaceExternalId,
            CurrentName = GetWorkspaceName(parameters.WorkspaceExternalId),
            NewName = parameters.Name
        };
    }

    private string? GetWorkspaceName(string workspaceExternalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT w_name
                     FROM w_workspaces
                     WHERE w_external_id = $externalId
                         AND w_is_being_deleted = FALSE
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$externalId", workspaceExternalId)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }
}
