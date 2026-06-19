using PlikShare.Agents.Operations;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Workspaces.Members.Invite.Contracts;

namespace PlikShare.Mcp.Workspaces.Members.Invite;

/// <summary>
/// Resolves an invite_workspace_members operation's stored parameters into the workspace's current
/// name plus the requested emails and share permission, so a human reviewing the approval sees exactly
/// who would gain access.
/// </summary>
public class InviteWorkspaceMembersOperationDetailsResolver(
    PlikShareDb plikShareDb)
{
    public InviteWorkspaceMembersOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<InviteWorkspaceMembersParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        return new InviteWorkspaceMembersOperationDetails
        {
            WorkspaceExternalId = parameters.WorkspaceExternalId,
            WorkspaceName = GetWorkspaceName(parameters.WorkspaceExternalId),
            MemberEmails = parameters.MemberEmails.ToList(),
            AllowShare = parameters.AllowShare
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
