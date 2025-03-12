using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmailV2;

namespace PlikShare.EmailProviders.ExternalProviders.AwsSes;

public static class AwsSesEmailSenderFactory
{
    public static AwsSesEmailSender Build(string emailFrom, AwsSesDetailsEntity details)
    {
        var client = new AmazonSimpleEmailServiceV2Client(
            credentials: new BasicAWSCredentials(
                accessKey: details.AccessKey,
                secretKey: details.SecretAccessKey),
            new AmazonSimpleEmailServiceV2Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(details.Region)
            });

        return new AwsSesEmailSender(
            emailFrom: emailFrom,
            sesClient: client);
    }
}