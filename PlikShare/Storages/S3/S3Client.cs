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
            Log.Error(e, "[S3:AWS] Something went wrong while creating client '{Region}:{AccessKey}'",
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
                ServiceURL = url,
                AuthenticationRegion = "auto"
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

            Log.Error(e, "[S3:CLOUDFLARE] Something went wrong while creating client '{Url}':'{AccessKeyId}'",
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

            Log.Error(e, "[S3:DIGITAL_OCEAN] Something went wrong while creating client '{Url}':'{AccessKeyId}'",
                url, accessKey);
            
            throw;
        }
    }

    public static AmazonS3Client BuildBackblazeClientOrThrow(
        string keyId,
        string applicationKey,
        string url)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = url,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };

        return new AmazonS3Client(
            new BasicAWSCredentials(
                accessKey: keyId,
                secretKey: applicationKey),
            config);
    }

    public static async Task<BackblazeResult> BuildBackblazeAndTestConnection(
        string keyId,
        string applicationKey,
        string url,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = BuildBackblazeClientOrThrow(
                keyId, applicationKey, url);

            var isConnectionOk = await TestConnection(
                client: client,
                cancellationToken: cancellationToken);

            if (!isConnectionOk)
                return new BackblazeResult(
                    Code: BackblazeResultCode.CouldNotConnect);


            return new BackblazeResult(
                Code: BackblazeResultCode.Ok,
                Client: client);
        }
        catch (Exception e)
        {
            if (e.Message.StartsWith("Value for ServiceURL is not a valid URL"))
            {
                return new BackblazeResult(Code: BackblazeResultCode.InvalidUrl);
            }

            Log.Error(e, "[S3:BACKBLAZE] Something went wrong while creating client '{Url}':'{AccessKeyId}'",
                url, keyId);

            throw;
        }
    }

    public static AmazonS3Client BuildGoogleCloudStorageClientOrThrow(
        string accessKey,
        string secretKey)
    {
        // AWSSDK 4.x defaults to `WHEN_SUPPORTED`, which attaches `x-amz-checksum-*`
        // headers GCS rejects as `ExcessHeaderValues`. Same workaround as Backblaze.
        var config = new AmazonS3Config
        {
            ServiceURL = "https://storage.googleapis.com",
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };

        return new AmazonS3Client(
            new BasicAWSCredentials(
                accessKey: accessKey,
                secretKey: secretKey),
            config);
    }

    public static async Task<GoogleCloudStorageResult> BuildGoogleCloudStorageAndTestConnection(
        string accessKey,
        string secretKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = BuildGoogleCloudStorageClientOrThrow(
                accessKey: accessKey,
                secretKey: secretKey);

            var isConnectionOk = await TestConnection(
                client: client,
                cancellationToken: cancellationToken);

            if (!isConnectionOk)
                return new GoogleCloudStorageResult(
                    Code: GoogleCloudStorageResultCode.CouldNotConnect);

            return new GoogleCloudStorageResult(
                Code: GoogleCloudStorageResultCode.Ok,
                Client: client);
        }
        catch (Exception e)
        {
            Log.Error(e, "[S3:GOOGLE_CLOUD_STORAGE] Something went wrong while creating client '{AccessKey}'",
                accessKey);

            throw;
        }
    }

    private static async Task<bool> TestConnection(
        IAmazonS3 client,
        CancellationToken cancellationToken = default)
    {
        var randomBucketName = $"test-bucket-{Guid.NewGuid():N}";

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
            Log.Warning(e, "[S3] Something went wrong while testing connection");

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
        IAmazonS3? Client = null);
    
    public enum CloudflareResultCode
    {
        Ok,
        InvalidUrl,
        CouldNotConnect,
    }

    public readonly record struct CloudflareResult(
        CloudflareResultCode Code,
        IAmazonS3? Client = null);
    
    public enum DigitalOceanSpacesResultCode
    {
        Ok,
        InvalidUrl,
        CouldNotConnect,
    }
    
    public readonly record struct DigitalOceanSpacesResult(
        DigitalOceanSpacesResultCode Code,
        IAmazonS3? Client = null);

    public enum BackblazeResultCode
    {
        Ok,
        InvalidUrl,
        CouldNotConnect,
    }

    public readonly record struct BackblazeResult(
        BackblazeResultCode Code,
        IAmazonS3? Client = null);

    public enum GoogleCloudStorageResultCode
    {
        Ok,
        CouldNotConnect,
    }

    public readonly record struct GoogleCloudStorageResult(
        GoogleCloudStorageResultCode Code,
        IAmazonS3? Client = null);
}