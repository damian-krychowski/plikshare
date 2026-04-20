using PlikShare.Boxes.Cache;
using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using PlikShare.Users.GetOrCreate;

namespace PlikShare.Boxes.Members.CreateInvitation;

public class CreateBoxMemberInvitationOperation(
    CreateBoxMemberInvitationQuery createBoxMemberInvitationQuery,
    GetOrCreateUserInvitationQuery getOrCreateUserInvitationQuery)
{
    public async Task<Result> Execute(
        BoxContext box,
        UserContext inviter,
        IEnumerable<Email> memberEmails,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var list = new List<CreateBoxMemberInvitationQuery.Member>();

        foreach (var email in memberEmails)
        {
            var (user, invitationCode) = await getOrCreateUserInvitationQuery.Execute(
                email: email,
                cancellationToken: cancellationToken);

            list.Add(new CreateBoxMemberInvitationQuery.Member(
                User: user,
                InvitationCode: invitationCode));
        }

        var members = list
            .ToArray();

        await createBoxMemberInvitationQuery.Execute(
            box: box,
            inviter: inviter,
            members: members,
            correlationId: correlationId,
            cancellationToken: cancellationToken);

        return new Result(
            Members: members.Select(m => m.User).ToArray());
    }

    public readonly record struct Result(
        UserContext[] Members);
}