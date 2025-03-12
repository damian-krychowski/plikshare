using PlikShare.Boxes.Cache;
using PlikShare.Users.Cache;
using PlikShare.Users.Entities;

namespace PlikShare.Boxes.Members.CreateInvitation;

public class CreateBoxMemberInvitationOperation(
    UserCache userCache,
    CreateBoxMemberInvitationQuery createBoxMemberInvitationQuery)
{
    public async Task<Result> Execute(
        BoxContext box,
        UserContext inviter,
        IEnumerable<Email> memberEmails,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var list = new List<UserContext>();

        foreach (var email in memberEmails)
        {
            var user = await userCache.GetOrCreateUserInvitationByEmail(
                email: email,
                cancellationToken: cancellationToken);

            list.Add(user);
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
            Members: members);
    }

    public readonly record struct Result(
        UserContext[] Members);
}