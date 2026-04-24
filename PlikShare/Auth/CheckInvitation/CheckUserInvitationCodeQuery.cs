using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Entities;
using PlikShare.Users.Invite;

namespace PlikShare.Auth.CheckInvitation;

public class CheckUserInvitationCodeQuery(PlikShareDb plikShareDb)
{
    public ResultCode Execute(
        string email,
        string invitationCode)
    {
        using var connection = plikShareDb.OpenConnection();

        var storedHash = connection
            .OneRowCmd(
                sql: @"
                    SELECT u_invitation_code_hash
                    FROM u_users
                    WHERE
                        u_normalized_email = $normalizedEmail
                        AND u_is_invitation = TRUE
                    LIMIT 1
                ",
                readRowFunc: reader => reader.GetFieldValue<byte[]>(0))
            .WithParameter("$normalizedEmail", Email.Normalize(email))
            .Execute();

        if (storedHash.IsEmpty)
            return ResultCode.WrongInvitationCode;

        if (!InvitationCodeHasher.TryHash(invitationCode, out var submittedHash))
            return ResultCode.WrongInvitationCode;

        return InvitationCodeHasher.FixedTimeEquals(submittedHash, storedHash.Value)
            ? ResultCode.Ok
            : ResultCode.WrongInvitationCode;
    }

    public enum ResultCode
    {
        Ok,
        WrongInvitationCode
    }
}
