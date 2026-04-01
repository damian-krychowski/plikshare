using Flurl;

namespace PlikShare.AuthProviders.Entities;

public static class OidcUrls
{
    public static string GetDiscoveryUrl(string issuerUrl)
    {
        return new Url(issuerUrl)
            .AppendPathSegment(".well-known/openid-configuration")
            .ToString();
    }
}
