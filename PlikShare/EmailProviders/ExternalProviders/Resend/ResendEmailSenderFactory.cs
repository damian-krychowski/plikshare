namespace PlikShare.EmailProviders.ExternalProviders.Resend;

public class ResendEmailSenderFactory
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public ResendEmailSenderFactory(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }
    
    public ResendEmailSender Build(string emailFrom, ResendDetailsEntity details)
    {
        var endpoint = _configuration["Resend:Endpoint"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException(
                "Cannot create ResendEmailSender because could not read Resend:Endpoint from configuration.");
        }
        
        return new ResendEmailSender(
            emailFrom: emailFrom,
            apiKey: details.ApiKey,
            resendEndpoint: endpoint,
            httpClientFactory: _httpClientFactory);
    }
}