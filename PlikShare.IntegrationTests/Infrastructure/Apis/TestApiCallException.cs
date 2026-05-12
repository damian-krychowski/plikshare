using PlikShare.Core.Utils;
using Serilog;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class TestApiCallException : Exception
{
    public TestApiCallException(string responseBody, int statusCode, string url)
    {
        ResponseBody = responseBody;
        StatusCode = statusCode;
        Url = url;
        HttpError = TryDeserializeHttpError(responseBody);

        Log.Error(
            "TestApiCallException: {Status} url={Url} body={Body}",
            statusCode,
            url,
            responseBody);
    }

    public string ResponseBody { get; }
    public int StatusCode { get; }
    public string Url { get; }
    public HttpError? HttpError { get; }

    private HttpError? TryDeserializeHttpError(string body)
    {
        try
        {
            return Json.Deserialize<HttpError>(body);
        }
        catch
        {
            return null;
        }
    }
}