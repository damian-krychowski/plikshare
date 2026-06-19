using PlikShare.Agents.Operations;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Boxes.Members.Invite.Contracts;

namespace PlikShare.Mcp.Boxes.Members.Invite;

/// <summary>
/// Resolves an invite_box_members operation's stored parameters into the box's current name plus the
/// requested emails, so a human reviewing the approval sees exactly who would gain access.
/// </summary>
public class InviteBoxMembersOperationDetailsResolver(
    PlikShareDb plikShareDb)
{
    public InviteBoxMembersOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<InviteBoxMembersParams>(operation.ParamsJson)
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

        return new InviteBoxMembersOperationDetails
        {
            WorkspaceExternalId = parameters.WorkspaceExternalId,
            BoxExternalId = parameters.BoxExternalId,
            BoxName = boxName.IsEmpty ? null : boxName.Value,
            MemberEmails = parameters.MemberEmails.ToList()
        };
    }
}
