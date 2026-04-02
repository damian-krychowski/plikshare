using Flurl;

namespace PlikShare.IntegrationTests.Infrastructure;

public static class UrlHelper
{
    public static string? ExtractQueryParam(string url, string paramName)
    {
        var parsedUrl = new Url(url);

        foreach (var param in parsedUrl.QueryParams)
        {
            if (param.Name == paramName)
                return param.Value?.ToString();
        }

        return null;
    }
}
