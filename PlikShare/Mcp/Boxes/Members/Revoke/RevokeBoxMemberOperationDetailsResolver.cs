using PlikShare.Agents.Operations;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Boxes.Members.Revoke.Contracts;

namespace PlikShare.Mcp.Boxes.Members.Revoke;

/// <summary>
/// Resolves a revoke_box_member operation's stored parameters into the box's current name and the member's
/// email, so a human reviewing the approval sees exactly who would lose access.
/// </summary>
public class RevokeBoxMemberOperationDetailsResolver(
    PlikShareDb plikShareDb)
{
    public RevokeBoxMemberOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<RevokeBoxMemberParams>(operation.ParamsJson)
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

        var memberEmail = connection
            .OneRowCmd(
                sql: "SELECT u_email FROM u_users WHERE u_external_id = $externalId LIMIT 1",
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$externalId", parameters.MemberExternalId)
            .Execute();

        return new RevokeBoxMemberOperationDetails
        {
            WorkspaceExternalId = parameters.WorkspaceExternalId,
            BoxExternalId = parameters.BoxExternalId,
            BoxName = boxName.IsEmpty ? null : boxName.Value,
            MemberExternalId = parameters.MemberExternalId,
            MemberEmail = memberEmail.IsEmpty ? null : memberEmail.Value
        };
    }
}
