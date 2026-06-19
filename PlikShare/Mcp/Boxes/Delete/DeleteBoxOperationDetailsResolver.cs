using PlikShare.Agents.Operations;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Boxes.Delete.Contracts;

namespace PlikShare.Mcp.Boxes.Delete;

/// <summary>
/// Resolves a delete_box operation's stored id into the box's current name, so a human reviewing the
/// approval sees exactly which box would be deleted.
/// </summary>
public class DeleteBoxOperationDetailsResolver(
    PlikShareDb plikShareDb)
{
    public DeleteBoxOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<DeleteBoxParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        using var connection = plikShareDb.OpenConnection();

        var boxName = connection
            .OneRowCmd(
                sql: """
                     SELECT bo_name
                     FROM bo_boxes
                     WHERE bo_external_id = $externalId
                         AND bo_is_being_deleted = FALSE
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$externalId", parameters.BoxExternalId)
            .Execute();

        return new DeleteBoxOperationDetails
        {
            WorkspaceExternalId = parameters.WorkspaceExternalId,
            BoxExternalId = parameters.BoxExternalId,
            BoxName = boxName.IsEmpty ? null : boxName.Value
        };
    }
}
