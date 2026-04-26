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
        var users = new List<GetOrCreateUserInvitationQuery.User>();

        foreach (var email in memberEmails)
        {
            var user = await getOrCreateUserInvitationQuery.Execute(
                email: email,
                cancellationToken: cancellationToken);
            
            users.Add(user);
        }

        var members = users
            .ToArray();

        await createBoxMemberInvitationQuery.Execute(
            box: box,
            inviter: inviter,
            members: users
                .Select(user => new CreateBoxMemberInvitationQuery.Member(
                    Id: user.Id,
                    Email: user.Email,
                    InvitationCode: user.InvitationCode))
                .ToArray(),
            correlationId: correlationId,
            cancellationToken: cancellationToken);

        return new Result(users);
    }

    public readonly record struct Result(
        List<GetOrCreateUserInvitationQuery.User> Members);
}