using System.IO.Compression;
using ProtoBuf;

namespace PlikShare.Core.Protobuf;

public class ProtobufResponseFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);

        return HandleResult(context, result);
    }

    private static object? HandleResult(
        EndpointFilterInvocationContext context, 
        object? result)
    {
        if (result is INestedHttpResult iNestedHttpResult)
            result = iNestedHttpResult.Result;

        if (result is null)
            return result;

        if (result is IStatusCodeHttpResult iStatusCodeHttpResult)
        {
            if (iStatusCodeHttpResult.StatusCode != StatusCodes.Status200OK)
                return result;
        }

        if (result is IValueHttpResult iValueHttpResult)
            return ToProtobuf(context, iValueHttpResult.Value);

        if (result is IResult)
            return result;

        return ToProtobuf(context, result);
    }

    private static IResult ToProtobuf(
        EndpointFilterInvocationContext context, 
        object? valueToSerialize)
    {
        var httpContext = context.HttpContext;

        httpContext.Response.Headers.ContentEncoding = "gzip";
        httpContext.Response.Headers.ContentType = "application/x-protobuf";

        using var responseStream = httpContext.Response.BodyWriter.AsStream();
        using var gzip = new GZipStream(responseStream, CompressionLevel.Optimal);

        Serializer.Serialize(gzip, valueToSerialize);

        return Results.Empty;
    }
}
public static class ProtobufFilterExtensions
{
    public static TBuilder WithProtobufResponse<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(new ProtobufResponseFilter());
        return builder;
    }
}

public static class HttpContextProtobufExtensions
{
    public static TRequest GetProtobufRequest<TRequest>(this HttpContext httpContext)
    {
        using var requestStream = httpContext.Request.BodyReader.AsStream();

        var request = Serializer.Deserialize<TRequest>(
            requestStream);

        return request;
    }
}