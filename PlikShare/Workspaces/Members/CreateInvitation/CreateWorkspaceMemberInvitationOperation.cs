using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Permissions;

namespace PlikShare.Workspaces.Members.CreateInvitation;

public class CreateWorkspaceMemberInvitationOperation(
    UserCache userCache,
    CreateWorkspaceMemberInvitationQuery createWorkspaceMemberInvitationQuery)
{
    public async Task<Result> Execute(
        WorkspaceContext workspace,
        UserContext inviter,
        IEnumerable<Email> memberEmails,
        WorkspacePermissions permission,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var list = new List<UserContext>();
        foreach (var email in memberEmails)
        {
            var user = await userCache.GetOrCreateUserInvitationByEmail(
                email,
                cancellationToken);

            list.Add(user);
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
            Members: members);
    }

    public readonly record struct Result(
        UserContext[]? Members = default);
}