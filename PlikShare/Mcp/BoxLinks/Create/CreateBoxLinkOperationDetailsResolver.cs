using PlikShare.Agents.Operations;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Mcp.BoxLinks.Create.Contracts;

namespace PlikShare.Mcp.BoxLinks.Create;

/// <summary>
/// Resolves a create_box_link operation's stored parameters into the box's current name plus the
/// requested link name, so a human reviewing the approval sees exactly what would be created and where.
/// </summary>
public class CreateBoxLinkOperationDetailsResolver(
    PlikShareDb plikShareDb)
{
    public CreateBoxLinkOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<CreateBoxLinkParams>(operation.ParamsJson)
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

        return new CreateBoxLinkOperationDetails
        {
            WorkspaceExternalId = parameters.WorkspaceExternalId,
            BoxExternalId = parameters.BoxExternalId,
            BoxName = boxName.IsEmpty ? null : boxName.Value,
            Name = parameters.Name
        };
    }
}
