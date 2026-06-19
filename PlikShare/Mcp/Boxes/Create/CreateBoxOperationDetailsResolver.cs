using PlikShare.Agents.Operations;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Boxes.Create.Contracts;

namespace PlikShare.Mcp.Boxes.Create;

/// <summary>
/// Resolves a create_box operation's stored parameters into the workspace's current name plus the
/// requested box name and folder, so a human reviewing the approval sees exactly what would be created.
/// </summary>
public class CreateBoxOperationDetailsResolver(
    PlikShareDb plikShareDb)
{
    public CreateBoxOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<CreateBoxParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        using var connection = plikShareDb.OpenConnection();

        var workspaceName = connection
            .OneRowCmd(
                sql: """
                     SELECT w_name
                     FROM w_workspaces
                     WHERE w_external_id = $externalId
                         AND w_is_being_deleted = FALSE
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$externalId", parameters.WorkspaceExternalId)
            .Execute();

        return new CreateBoxOperationDetails
        {
            WorkspaceExternalId = parameters.WorkspaceExternalId,
            WorkspaceName = workspaceName.IsEmpty ? null : workspaceName.Value,
            Name = parameters.Name,
            FolderExternalId = parameters.FolderExternalId
        };
    }
}
