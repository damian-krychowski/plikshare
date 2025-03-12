using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using PlikShare.Core.Emails;
using PlikShare.Users.Entities;
using Serilog;

namespace PlikShare.EmailProviders.ExternalProviders.AwsSes;

public class AwsSesEmailSender: IEmailSender
{
    private readonly string _emailFrom;
    private readonly IAmazonSimpleEmailServiceV2 _sesClient;

    public AwsSesEmailSender(
        string emailFrom,
        IAmazonSimpleEmailServiceV2 sesClient)
    {
        _emailFrom = emailFrom;
        _sesClient = sesClient;
    }
    
    public async Task SendEmail(
        string to, 
        string subject, 
        string htmlContent, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = GetRequest(
                to, 
                subject, 
                htmlContent);
            
            var response = await _sesClient.SendEmailAsync(
                request: request,
                cancellationToken: cancellationToken);
            
            Log.Information("[AWS SES] Email to '{Email}' ('{Subject}') was sent ({MessageId})",
                EmailAnonymization.Anonymize(to),
                subject,
                response.MessageId);
        }
        catch (AccountSuspendedException ex)
        {
            Log.Error(ex, "[AWS SES] Sending email to '{Email}' ('{Subject}') failed. The account's ability to send email has been permanently restricted.",
                EmailAnonymization.Anonymize(to),
                subject);

            throw;
        }
        catch (MailFromDomainNotVerifiedException ex)
        {
            Log.Error(ex, "[AWS SES] Sending email to '{Email}' ('{Subject}') failed. The sending domain is not verified.",
                EmailAnonymization.Anonymize(to),
                subject);

            throw;
        }
        catch (MessageRejectedException ex)
        {
            Log.Error(ex, "[AWS SES] Sending email to '{Email}' ('{Subject}') failed. The message content is invalid.",
                EmailAnonymization.Anonymize(to),
                subject);

            throw;
        }
        catch (SendingPausedException ex)
        {
            Log.Error(ex, "[AWS SES] Sending email to '{Email}' ('{Subject}') failed. The account's ability to send email is currently paused.",
                EmailAnonymization.Anonymize(to),
                subject);

            throw;
        }
        catch (TooManyRequestsException ex)
        {
            Log.Error(ex, "[AWS SES] Sending email to '{Email}' ('{Subject}') failed. Too many requests were made. Please try again later.",
                EmailAnonymization.Anonymize(to),
                subject);

            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[AWS SES] Sending email to '{Email}' ('{Subject}') failed. An error occurred while sending the email.",
                EmailAnonymization.Anonymize(to),
                subject);

            throw;
        }
    }

    private SendEmailRequest GetRequest(string to, string subject, string htmlContent)
    {
        var request = new SendEmailRequest
        {
            FromEmailAddress = _emailFrom,
            Destination = new Destination
            {
                ToAddresses = [to]
            },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content
                    {
                        Data = subject
                    },
                    Body = new Body
                    {
                        Html = new Content
                        {
                            Data = htmlContent
                        }
                    }
                }
            }
        };
        return request;
    }
}