using PlikShare.Core.Configuration;

namespace PlikShare.QuickShares;

public class QuickShareUrlBuilder(IConfig config)
{
    public string BuildUrl(string accessCode) => $"{config.AppUrl}/share/{accessCode}";
}
