using System.IO.Compression;
using ProtoBuf;

namespace PlikShare.Core.Protobuf;

public class ProtobufStreamResult<TChunk>(IEnumerable<TChunk> chunks) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.Headers.ContentEncoding = "gzip";
        httpContext.Response.Headers.ContentType = "application/x-protobuf";

        await using var responseStream = httpContext.Response.BodyWriter.AsStream();
        await using var gzip = new GZipStream(
            responseStream,
            CompressionLevel.Fastest);

        try
        {
            foreach (var chunk in chunks)
            {
                Serializer.Serialize(
                    gzip,
                    chunk);

                await gzip.FlushAsync(
                    httpContext.RequestAborted);
            }
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
        }
    }
}
