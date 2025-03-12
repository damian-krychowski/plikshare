using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Entities;

namespace PlikShare.Auth.CheckInvitation;

public class CheckUserInvitationCodeQuery(PlikShareDb plikShareDb)
{
    public ResultCode Execute(
        string email,
        string invitationCode)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: @"
                    SELECT u_invitation_code
                    FROM u_users
                    WHERE 
                        u_normalized_email = $normalizedEmail
                        AND u_is_invitation = TRUE
                    LIMIT 1
                ",
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$normalizedEmail", Email.Normalize(email))
            .Execute();

        if (result.IsEmpty)
            return ResultCode.WrongInvitationCode;

        return invitationCode.Equals(result.Value) 
            ? ResultCode.Ok 
            : ResultCode.WrongInvitationCode;
    }

    public enum ResultCode
    {
        Ok,
        WrongInvitationCode
    }
}