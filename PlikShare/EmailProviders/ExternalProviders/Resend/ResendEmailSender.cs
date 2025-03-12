using System.Text;
using Microsoft.Net.Http.Headers;
using PlikShare.Core.Emails;
using PlikShare.Core.Utils;
using Serilog;

namespace PlikShare.EmailProviders.ExternalProviders.Resend;

public class ResendEmailSender(
    string emailFrom,
    string apiKey,
    string resendEndpoint,
    IHttpClientFactory httpClientFactory): IEmailSender
{
    public async Task SendEmail(
        string to,
        string subject,
        string htmlContent, 
        CancellationToken cancellationToken = default)
    {
        var request = GetRequestMessage(
            to,
            subject,
            htmlContent);

        try
        {
            using var httpClient = httpClientFactory
                .CreateClient();

            var httpResponseMessage = await httpClient
                .SendAsync(request, cancellationToken);

            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                //todo improve exception (maybe should not be exception but result type?)
                throw new InvalidOperationException("Cannot send email");
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Cannot send email '{Subject}' to '{Recipient}'", subject, to);
            
            throw;
        }
    }

    private HttpRequestMessage GetRequestMessage(
        string to,
        string subject,
        string htmlContent)
    {
        var requestBody = new ResendRequestBody(
            From: emailFrom,
            To: [to],
            Subject: subject,
            Html: htmlContent);

        return new HttpRequestMessage(
            HttpMethod.Post,
            resendEndpoint)
        {
            Headers =
            {
                { HeaderNames.Authorization, $"Bearer {apiKey}" }
            },

            Content = new StringContent(
                Json.Serialize(item: requestBody),
                Encoding.UTF8,
                "application/json")
        };
    }
}