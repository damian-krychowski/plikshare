using PlikShare.Agents.Operations;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Workspaces.Members.UpdatePermissions.Contracts;

namespace PlikShare.Mcp.Workspaces.Members.UpdatePermissions;

/// <summary>
/// Resolves an update_workspace_member_permissions operation's stored parameters into the workspace's
/// current name and the member's email, so a human reviewing the approval sees exactly whose permissions
/// would change and to what.
/// </summary>
public class UpdateWorkspaceMemberPermissionsOperationDetailsResolver(
    PlikShareDb plikShareDb)
{
    public UpdateWorkspaceMemberPermissionsOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<UpdateWorkspaceMemberPermissionsParams>(operation.ParamsJson)
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

        var memberEmail = connection
            .OneRowCmd(
                sql: "SELECT u_email FROM u_users WHERE u_external_id = $externalId LIMIT 1",
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$externalId", parameters.MemberExternalId)
            .Execute();

        return new UpdateWorkspaceMemberPermissionsOperationDetails
        {
            WorkspaceExternalId = parameters.WorkspaceExternalId,
            WorkspaceName = workspaceName.IsEmpty ? null : workspaceName.Value,
            MemberExternalId = parameters.MemberExternalId,
            MemberEmail = memberEmail.IsEmpty ? null : memberEmail.Value,
            AllowShare = parameters.AllowShare
        };
    }
}
