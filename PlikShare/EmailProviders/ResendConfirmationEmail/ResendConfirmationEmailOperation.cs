using PlikShare.EmailProviders.EmailSender;
using PlikShare.EmailProviders.Id;
using PlikShare.EmailProviders.SendConfirmationEmail;
using PlikShare.Users.Entities;
using Serilog;

namespace PlikShare.EmailProviders.ResendConfirmationEmail;

public class ResendConfirmationEmailOperation(
    EmailProviderConfirmationEmail emailProviderConfirmationEmail,
    EmailSenderFactory emailSenderFactory,
    GetEmailProviderQuery getEmailProviderQuery)
{
    public async Task<ResultCode> Execute(
        EmailProviderExtId externalId,
        Email emailTo,
        CancellationToken cancellationToken = default)
    {
        var result = getEmailProviderQuery.Execute(
            externalId: externalId);

        if (result.Code == GetEmailProviderQuery.ResultCode.NotFound)
            return ResultCode.NotFound;

        if (result.EmailProvider!.IsConfirmed)
            return ResultCode.AlreadyConfirmed;
        
        try
        {
            var emailSender = emailSenderFactory.Build(
                emailProviderType: result.EmailProvider.Type,
                emailFrom: result.EmailProvider.EmailFrom,
                detailsJson: result.EmailProvider.DetailsJson);
            
            await emailProviderConfirmationEmail.Send(
                emailProviderName: result.EmailProvider.Name,
                confirmationCode: result.EmailProvider.ConfirmationCode,
                to: emailTo,
                emailSender: emailSender,
                cancellationToken: cancellationToken);

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while resending Email Provider confirmation email.");

            return ResultCode.CouldNotSendTestEmail;
        }
    }
    
    public enum ResultCode
    {
        Ok,
        NotFound,
        AlreadyConfirmed,
        CouldNotSendTestEmail
    }
}