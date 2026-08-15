using Amazon.S3;
using Amazon.S3.Model;
using Serilog;

namespace PlikShare.Storages.S3.S3Compatible;

/// <summary>
/// Best-effort bucket CORS setup for generic S3-compatible backends. Support for the
/// <c>PutBucketCors</c> API varies (e.g. MinIO handles CORS at the server level and
/// historically rejected bucket-level configuration), so a failure here must not fail
/// the whole bucket provisioning — browser uploads still work whenever the backend
/// allows the app origin through its server-wide CORS configuration.
/// </summary>
public static class S3CompatibleCorsConfigurer
{
    public static async Task TryPutBucketCors(
        IAmazonS3 client,
        string bucketName,
        string allowedOrigin,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.PutCORSConfigurationAsync(new PutCORSConfigurationRequest
            {
                BucketName = bucketName,
                Configuration = new CORSConfiguration
                {
                    Rules =
                    [
                        new CORSRule
                        {
                            AllowedMethods = ["GET", "PUT"],
                            AllowedHeaders = ["*"],
                            ExposeHeaders = ["Etag"],
                            AllowedOrigins = [allowedOrigin]
                        }
                    ]
                }
            }, cancellationToken);

            Log.Information("[S3:S3_COMPATIBLE] CORS for Bucket '{BucketName}' was set. AllowedOrigins '{AllowedOrigins}'",
                bucketName, allowedOrigin);
        }
        catch (Exception e)
        {
            Log.Warning(e,
                "[S3:S3_COMPATIBLE] Could not set bucket-level CORS for '{BucketName}'. " +
                "If browser uploads fail with CORS errors, configure your S3 server to allow the origin '{AllowedOrigin}'.",
                bucketName, allowedOrigin);
        }
    }
}
