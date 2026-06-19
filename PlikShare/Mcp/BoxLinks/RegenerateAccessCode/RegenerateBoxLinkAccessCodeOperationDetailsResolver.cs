using PlikShare.Agents.Operations;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Mcp.BoxLinks.RegenerateAccessCode.Contracts;

namespace PlikShare.Mcp.BoxLinks.RegenerateAccessCode;

/// <summary>
/// Resolves a regenerate_box_link_access_code operation's stored id into the box link's current name, so a
/// human reviewing the approval sees exactly which link's URL would be invalidated.
/// </summary>
public class RegenerateBoxLinkAccessCodeOperationDetailsResolver(
    PlikShareDb plikShareDb)
{
    public RegenerateBoxLinkAccessCodeOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<RegenerateBoxLinkAccessCodeParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        using var connection = plikShareDb.OpenConnection();

        var boxLinkName = connection
            .OneRowCmd(
                sql: "SELECT bl_name FROM bl_box_links WHERE bl_external_id = $externalId LIMIT 1",
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$externalId", parameters.BoxLinkExternalId)
            .Execute();

        return new RegenerateBoxLinkAccessCodeOperationDetails
        {
            WorkspaceExternalId = parameters.WorkspaceExternalId,
            BoxLinkExternalId = parameters.BoxLinkExternalId,
            BoxLinkName = boxLinkName.IsEmpty ? null : boxLinkName.Value
        };
    }
}
