
namespace PlikShare.Core.Emails;

public class EmailProviderStore
{
    private int? _emailProviderId = null;
    private volatile IEmailSender? _emailSender = null;

    public IEmailSender? EmailSender => _emailSender;

    public bool IsEmailSenderAvailable => _emailSender is not null;
    
    public void SetEmailSender(int emailProviderId, IEmailSender? emailSender)
    {
        _emailProviderId = emailProviderId;
        _emailSender = emailSender;
    }

    public void TryRemove(int emailProviderId)
    {
        if(_emailProviderId != emailProviderId)
            return;

        _emailSender = null;
    }
}