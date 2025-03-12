using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Serilog;

namespace PlikShare.Storages.S3;

public static class S3Client
{
    public static AmazonS3Client BuildAwsClientOrThrow(
        string accessKey,
        string secretAccessKey,
        string region)
    {
        return new AmazonS3Client(
            new BasicAWSCredentials(
                accessKey: accessKey,
                secretKey: secretAccessKey),
            new AmazonS3Config
            {
                ServiceURL = "https://s3.amazonaws.com",
                RegionEndpoint = RegionEndpoint.GetBySystemName(region)
            });
    }
    
    public static async Task<AwsResult> BuildAwsAndTestConnection(
        string accessKey, 
        string secretAccessKey, 
        string region,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = BuildAwsClientOrThrow(
                accessKey: accessKey,
                secretAccessKey: secretAccessKey, 
                region: region);

            var isConnectionOk = await TestConnection(
                client: client,
                cancellationToken: cancellationToken);

            if (!isConnectionOk)
                return new AwsResult(
                    Code: AwsResultCode.CouldNotConnect);
            
            return new AwsResult(
                Code: AwsResultCode.Ok,
                Client: client);
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while creating Amazon S3 client '{Region}:{AccessKey}'",
                region, accessKey);
            
            throw;
        }
    }
    
    public static AmazonS3Client BuildCloudflareClientOrThrow(
        string accessKeyId,
        string secretAccessKey,
        string url)
    {
        return new AmazonS3Client(
            new BasicAWSCredentials(
                accessKey: accessKeyId,
                secretKey: secretAccessKey),
            new AmazonS3Config
            {
                ServiceURL = url
            });
    }
    
    public static async Task<CloudflareResult> BuildCloudflareAndTestConnection(
        string accessKeyId, 
        string secretAccessKey, 
        string url,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = BuildCloudflareClientOrThrow(
                accessKeyId: accessKeyId,
                secretAccessKey: secretAccessKey, 
                url: url);

            var isConnectionOk = await TestConnection(
                client: client,
                cancellationToken: cancellationToken);

            if (!isConnectionOk)
                return new CloudflareResult(
                    Code: CloudflareResultCode.CouldNotConnect);

         
            return new CloudflareResult(
                Code: CloudflareResultCode.Ok,
                Client: client);
        }
        catch (Exception e)
        {
            if (e.Message.StartsWith("Value for ServiceURL is not a valid URL"))
            {
                return new CloudflareResult(Code: CloudflareResultCode.InvalidUrl);
            }

            Log.Error(e, "Something went wrong while creating Amazon S3 client '{Url}':'{AccessKeyId}'",
                url, accessKeyId);
            
            throw;
        }
    }
    
    public static AmazonS3Client BuildDigitalOceanSpacesClientOrThrow(
        string accessKey,
        string secretKey,
        string url)
    {
        return new AmazonS3Client(
            new BasicAWSCredentials(
                accessKey: accessKey,
                secretKey: secretKey),
            new AmazonS3Config
            {
                ServiceURL = url
            });
    }
    
    public static async Task<DigitalOceanSpacesResult> BuildDigitalOceanSpacesAndTestConnection(
        string accessKey, 
        string secretKey, 
        string url,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = BuildDigitalOceanSpacesClientOrThrow(
                accessKey: accessKey,
                secretKey: secretKey, 
                url: url);

            var isConnectionOk = await TestConnection(
                client: client,
                cancellationToken: cancellationToken);

            if (!isConnectionOk)
                return new DigitalOceanSpacesResult(
                    Code: DigitalOceanSpacesResultCode.CouldNotConnect);
            
            return new DigitalOceanSpacesResult(
                Code: DigitalOceanSpacesResultCode.Ok,
                Client: client);
        }
        catch (Exception e)
        {
            if (e.Message.StartsWith("Value for ServiceURL is not a valid URL"))
            {
                return new DigitalOceanSpacesResult(Code: DigitalOceanSpacesResultCode.InvalidUrl);
            }

            Log.Error(e, "Something went wrong while creating Amazon S3 client '{Url}':'{AccessKeyId}'",
                url, accessKey);
            
            throw;
        }
    }
    
    private static async Task<bool> TestConnection(
        IAmazonS3 client,
        CancellationToken cancellationToken = default)
    {
        var randomBucketName = $"test-bucket-{Guid.NewGuid()}";

        try
        {
            await client.PutBucketAsync(new PutBucketRequest
            {
                BucketName = randomBucketName
            }, cancellationToken);

            await client.DeleteBucketAsync(new DeleteBucketRequest
            {
                BucketName = randomBucketName
            }, cancellationToken);

            return true;
        }
        catch (Exception e)
        {
            Log.Warning(e, "Something went wrong while testing Cloudflare R2 connection");

            return false;
        }
    }
    
    public enum AwsResultCode
    {
        Ok,
        CouldNotConnect,
    }

    public readonly record struct AwsResult(
        AwsResultCode Code,
        IAmazonS3? Client = default);
    
    public enum CloudflareResultCode
    {
        Ok,
        InvalidUrl,
        CouldNotConnect,
    }

    public readonly record struct CloudflareResult(
        CloudflareResultCode Code,
        IAmazonS3? Client = default);
    
    public enum DigitalOceanSpacesResultCode
    {
        Ok,
        InvalidUrl,
        CouldNotConnect,
    }
    
    public readonly record struct DigitalOceanSpacesResult(
        DigitalOceanSpacesResultCode Code,
        IAmazonS3? Client = default);
}