
using Flurl.Http;
using PlikShare.Core.Authorization;
using ProtoBuf;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public static class FlurlRequestExtensions
{
    public static IFlurlRequest WithCookie(
        this IFlurlRequest request,
        Cookie? cookie)
    {
        if (cookie is not null)
        {
            return request.WithCookie(
                name: cookie.Name,
                value: cookie.Value);
        }

        return request;
    }

    public static IFlurlRequest WithHeader(
        this IFlurlRequest request,
        string name,
        string value)
    {
        request.Headers.Add(name, value);

        return request;
    }

    public static IFlurlRequest WithHeaders(
        this IFlurlRequest request,
        List<Header>? headers)
    {
        if (headers is null)
            return request;

        foreach (var header in headers)
        {
            request.Headers.Add(header.Name, header.Value);
        }

        return request;
    }

    public static IFlurlRequest WithAntiforgeryHeader(
        this IFlurlRequest request,
        string value)
    {
        return request.WithHeader(
            name: HeaderName.Antiforgery,
            value: value);
    }

    public static IFlurlRequest WithAntiforgery(
        this IFlurlRequest request,
        AntiforgeryCookies? antiforgeryCookies)
    {
        if (antiforgeryCookies is null)
            return request;

        return request
            .WithCookie(antiforgeryCookies.AspNetAntiforgery)
            .WithAntiforgeryHeader(antiforgeryCookies.AntiforgeryToken.Value);
    }

    public static async Task<TResponse> ExecuteGet<TResponse>(
        this IFlurlClient client,
        string appUrl,
        string apiPath,
        Cookie? cookie,
        bool isResponseInProtobuf = false,
        List<Header>? headers = null)
    {
        var response = await client
            .Request(appUrl, apiPath)
            .AllowAnyHttpStatus()
            .WithCookie(cookie)
            .WithHeaders(headers)
            .GetAsync();

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            var exception = new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);

            throw exception;
        }

        TResponse? responseDeserialized;

        if (isResponseInProtobuf)
        {
            await using var responseStream = await response.GetStreamAsync();

            responseDeserialized = Serializer.Deserialize<TResponse>(
                responseStream);
        }
        else
        {
            responseDeserialized = await response.GetJsonAsync<TResponse>();
        }

        if (responseDeserialized is null)
        {
            throw new InvalidOperationException(
                $"Request to '{apiPath}' succeeded but deserialization of the response is null");
        }

        return responseDeserialized;
    }
    
    public static async Task<TResponse> ExecutePost<TResponse, TRequest>(
        this IFlurlClient client,
        string appUrl,
        string apiPath,
        TRequest request,
        Cookie? cookie,
        AntiforgeryCookies antiforgery,
        bool isRequestInProtobuf = false,
        bool isResponseInProtobuf = false,
        List<Header>? headers = null)
    {
        var flurlRequest = client
            .Request(appUrl, apiPath)
            .WithAntiforgery(antiforgery)
            .AllowAnyHttpStatus()
            .WithHeaders(headers)
            .WithCookie(cookie);
        
        IFlurlResponse? response;

        if (isRequestInProtobuf)
        {
            using var memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, request);

            memoryStream.Seek(0, SeekOrigin.Begin);
            response = await flurlRequest.PostAsync(new StreamContent(memoryStream));
        }
        else
        {
            response = await flurlRequest.PostJsonAsync(request);
        }

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            var exception = new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);

            throw exception;
        }

        TResponse? responseDeserialized;

        if (isResponseInProtobuf)
        {
            await using var responseStream = await response.GetStreamAsync();

            responseDeserialized = Serializer.Deserialize<TResponse>(
                responseStream);
        }
        else
        {
            responseDeserialized = await response.GetJsonAsync<TResponse>();
        }

        if (responseDeserialized is null)
        {
            throw new InvalidOperationException(
                $"Request to '{apiPath}' succeeded but deserialization of the response is null");
        }

        return responseDeserialized;
    }

    public static async Task ExecutePost<TRequest>(
        this IFlurlClient client,
        string appUrl,
        string apiPath,
        TRequest request,
        Cookie? cookie,
        AntiforgeryCookies antiforgery,
        List<Header>? headers = null)
    {
        var response = await client
            .Request(appUrl, apiPath)
            .AllowAnyHttpStatus()
            .WithCookie(cookie)
            .WithAntiforgery(antiforgery)
            .WithHeaders(headers)
            .PostJsonAsync(request);

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            var exception = new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);

            throw exception;
        }
    }
    
    public static async Task<TResponse> ExecutePatch<TResponse, TRequest>(
        this IFlurlClient client,
        string appUrl,
        string apiPath,
        TRequest request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery,
        List<Header>? headers = null)
    {
        var response = await client
            .Request(appUrl, apiPath)
            .AllowAnyHttpStatus()
            .WithCookie(cookie)
            .WithAntiforgery(antiforgery)
            .WithHeaders(headers)
            .PatchJsonAsync(request);

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            var exception = new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);

            throw exception;
        }

        var responseDeserialized = await response.GetJsonAsync<TResponse>();

        if (responseDeserialized is null)
        {
            throw new InvalidOperationException(
                $"Request to '{apiPath}' succeeded but deserialization of the response is null");
        }

        return responseDeserialized;
    }
    
    public static async Task ExecutePatch<TRequest>(
        this IFlurlClient client,
        string appUrl,
        string apiPath,
        TRequest request,
        Cookie? cookie,
        AntiforgeryCookies? antiforgery,
        List<Header>? headers = null)
    {
        var response = await client
            .Request(appUrl, apiPath)
            .AllowAnyHttpStatus()
            .WithCookie(cookie)
            .WithAntiforgery(antiforgery)
            .WithHeaders(headers)
            .PatchJsonAsync(request);

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            var exception = new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);

            throw exception;
        }
    }

    public static async Task ExecuteDelete(
        this IFlurlClient client,
        string appUrl,
        string apiPath,
        Cookie? cookie,
        AntiforgeryCookies? antiforgery,
        List<Header>? headers = null)
    {
        var response = await client
            .Request(appUrl, apiPath)
            .AllowAnyHttpStatus()
            .WithCookie(cookie)
            .WithAntiforgery(antiforgery)
            .WithHeaders(headers)
            .DeleteAsync();

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            var exception = new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);

            throw exception;
        }
    }
}