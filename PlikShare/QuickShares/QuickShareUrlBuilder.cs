using PlikShare.Core.Configuration;

namespace PlikShare.QuickShares;

public class QuickShareUrlBuilder(IConfig config)
{
    public string BuildUrl(string slug, string? secretToken = null)
    {
        var url = $"{config.AppUrl}/share/{slug}";
        return secretToken is null ? url : $"{url}?token={Uri.EscapeDataString(secretToken)}";
    }
}
