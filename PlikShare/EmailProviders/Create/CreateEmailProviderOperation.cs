using PlikShare.EmailProviders.Delete;
using PlikShare.EmailProviders.EmailSender;
using PlikShare.EmailProviders.Entities;
using PlikShare.EmailProviders.Id;
using PlikShare.EmailProviders.SendConfirmationEmail;
using PlikShare.Users.Cache;

namespace PlikShare.EmailProviders.Create;

public class CreateEmailProviderOperation(
    EmailProviderConfirmationEmail emailProviderConfirmationEmail,
    EmailSenderFactory emailSenderFactory,
    CreateEmailProviderQuery createEmailProviderQuery,
    DeleteEmailProviderQuery deleteEmailProviderQuery)
{
    public async Task<Result> Execute(
        string name,
        EmailProviderType type,
        string emailFrom,
        string detailsJson,
        UserContext user,
        CancellationToken cancellationToken)
    {
        var result = await createEmailProviderQuery.Execute(
            name: name,
            type: type, 
            emailFrom: emailFrom,
            detailsJson: detailsJson,
            cancellationToken: cancellationToken);

        if (result.Code == CreateEmailProviderQuery.ResultCode.NameNotUnique)
            return new Result(Code: ResultCode.NameNotUnique);

        try
        {
            var emailSender = emailSenderFactory.Build(
                emailProviderType: type,
                emailFrom: emailFrom,
                detailsJson: detailsJson);
            
            await emailProviderConfirmationEmail.Send(
                emailProviderName: name,
                confirmationCode: result.EmailProvider!.ConfirmationCode,
                to: user.Email,
                emailSender: emailSender,
                cancellationToken: cancellationToken);
            
            return new Result(
                Code: ResultCode.Ok,
                EmailProviderExternalId: result.EmailProvider.ExternalId);
        }
        catch (Exception e)
        {
            await deleteEmailProviderQuery.Execute(
                result.EmailProvider!.ExternalId,
                cancellationToken: cancellationToken);

            return new Result(
                Code: ResultCode.CouldNotSendTestEmail,
                InnerError: e.Message);
        }
    }

    public enum ResultCode
    {
        Ok,
        CouldNotSendTestEmail,
        NameNotUnique
    }
    
    public record Result(
        ResultCode Code,
        EmailProviderExtId? EmailProviderExternalId = null,
        string? InnerError = null);
}