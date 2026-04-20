using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using PlikShare.Users.GetOrCreate;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Permissions;

namespace PlikShare.Workspaces.Members.CreateInvitation;

public class CreateWorkspaceMemberInvitationOperation(
    CreateWorkspaceMemberInvitationQuery createWorkspaceMemberInvitationQuery,
    GetOrCreateUserInvitationQuery getOrCreateUserInvitationQuery)
{
    public async Task<Result> Execute(
        WorkspaceContext workspace,
        UserContext inviter,
        IEnumerable<Email> memberEmails,
        WorkspacePermissions permission,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var list = new List<CreateWorkspaceMemberInvitationQuery.Member>();

        foreach (var email in memberEmails)
        {
            var (user, invitationCode) = await getOrCreateUserInvitationQuery.Execute(
                email,
                cancellationToken);

            list.Add(new CreateWorkspaceMemberInvitationQuery.Member(
                User: user,
                InvitationCode: invitationCode));
        }

        var members = list
            .ToArray();

        await createWorkspaceMemberInvitationQuery.Execute(
            workspace: workspace,
            inviter: inviter,
            members: members,
            allowShare: permission.AllowShare,
            correlationId: correlationId,
            cancellationToken: cancellationToken);

        return new Result(
            Members: members.Select(m => m.User).ToArray());
    }

    public readonly record struct Result(
        UserContext[]? Members = default);
}