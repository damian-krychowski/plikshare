using PlikShare.Core.Utils;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class TestApiCallException : Exception
{
    public TestApiCallException(string responseBody, int statusCode)
    {
        ResponseBody = responseBody;
        StatusCode = statusCode;
        HttpError = TryDeserializeHttpError(responseBody);
    }

    public string ResponseBody { get; }
    public int StatusCode { get; }
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