using PlikShare.Agents.Operations;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Mcp.ShareLinks.Update.Contracts;

namespace PlikShare.Mcp.ShareLinks.Update;

/// <summary>
/// Resolves an update_share_link operation's stored parameters into the share link's current name and
/// the requested setting changes (rename, expiry, download limit, password), so a human reviewing the
/// approval sees exactly what would change.
/// </summary>
public class UpdateShareLinkOperationDetailsResolver(
    PlikShareDb plikShareDb)
{
    public UpdateShareLinkOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<UpdateShareLinkParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        return new UpdateShareLinkOperationDetails
        {
            ShareLinkExternalId = parameters.ShareLinkExternalId,
            CurrentName = GetShareLinkName(parameters.ShareLinkExternalId),
            UpdateName = parameters.UpdateName,
            NewName = parameters.Name,
            UpdateExpiration = parameters.UpdateExpiration,
            ExpiresAt = parameters.ExpiresAt?.ToString("O"),
            UpdateMaxDownloads = parameters.UpdateMaxDownloads,
            MaxDownloads = parameters.MaxDownloads,
            UpdatePassword = parameters.UpdatePassword,
            PasswordSet = parameters.PasswordSet
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
