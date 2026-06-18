using PlikShare.Agents.Operations;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Mcp.ShareLinks.Delete.Contracts;

namespace PlikShare.Mcp.ShareLinks.Delete;

/// <summary>
/// Resolves a delete_share_link operation's stored share link id into its name, so a human reviewing
/// the approval sees which public link the agent wants to revoke.
/// </summary>
public class DeleteShareLinkOperationDetailsResolver(
    PlikShareDb plikShareDb)
{
    public DeleteShareLinkOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<DeleteShareLinkParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        return new DeleteShareLinkOperationDetails
        {
            ExternalId = parameters.ShareLinkExternalId,
            Name = GetShareLinkName(parameters.ShareLinkExternalId)
        };
    }

    private string? GetShareLinkName(string shareLinkExternalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT qsh_name
                     FROM qsh_quick_shares
                     WHERE qsh_external_id = $externalId
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$externalId", shareLinkExternalId)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }
}
