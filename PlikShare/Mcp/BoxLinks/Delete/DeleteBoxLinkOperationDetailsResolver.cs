using PlikShare.Agents.Operations;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Mcp.BoxLinks.Delete.Contracts;

namespace PlikShare.Mcp.BoxLinks.Delete;

/// <summary>
/// Resolves a delete_box_link operation's stored id into the box link's current name, so a human reviewing
/// the approval sees exactly which link would be deleted.
/// </summary>
public class DeleteBoxLinkOperationDetailsResolver(
    PlikShareDb plikShareDb)
{
    public DeleteBoxLinkOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<DeleteBoxLinkParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        using var connection = plikShareDb.OpenConnection();

        var boxLinkName = connection
            .OneRowCmd(
                sql: "SELECT bl_name FROM bl_box_links WHERE bl_external_id = $externalId LIMIT 1",
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$externalId", parameters.BoxLinkExternalId)
            .Execute();

        return new DeleteBoxLinkOperationDetails
        {
            WorkspaceExternalId = parameters.WorkspaceExternalId,
            BoxLinkExternalId = parameters.BoxLinkExternalId,
            BoxLinkName = boxLinkName.IsEmpty ? null : boxLinkName.Value
        };
    }
}
