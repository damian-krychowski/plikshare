namespace PlikShare.IntegrationTests.Infrastructure.Storage;

/// <summary>
/// Reads S3 provider credentials from environment variables. Throws if a required
/// variable is missing — these tests run live against real cloud storage and must
/// fail loudly rather than silently skip.
/// </summary>
public static class S3StorageConfig
{
    public static AwsS3Credentials AwsS3 => new(
        AccessKey: Required("PLIKSHARE_TEST_AWS_S3_ACCESS_KEY"),
        SecretAccessKey: Required("PLIKSHARE_TEST_AWS_S3_SECRET_KEY"),
        Region: Required("PLIKSHARE_TEST_AWS_S3_REGION"));

    public static CloudflareR2Credentials CloudflareR2 => new(
        AccessKeyId: Required("PLIKSHARE_TEST_R2_ACCESS_KEY_ID"),
        SecretAccessKey: Required("PLIKSHARE_TEST_R2_SECRET_ACCESS_KEY"),
        Url: Required("PLIKSHARE_TEST_R2_URL"));

    public static BackblazeB2Credentials BackblazeB2 => new(
        KeyId: Required("PLIKSHARE_TEST_B2_KEY_ID"),
        ApplicationKey: Required("PLIKSHARE_TEST_B2_APPLICATION_KEY"),
        Url: Required("PLIKSHARE_TEST_B2_URL"));

    public static DigitalOceanSpacesCredentials DigitalOceanSpaces => new(
        AccessKey: Required("PLIKSHARE_TEST_DO_ACCESS_KEY"),
        SecretKey: Required("PLIKSHARE_TEST_DO_SECRET_KEY"),
        Region: Required("PLIKSHARE_TEST_DO_REGION"));

    public static AzureBlobCredentials AzureBlob => new(
        AccountName: Required("PLIKSHARE_TEST_AZURE_BLOB_ACCOUNT_NAME"),
        AccountKey: Required("PLIKSHARE_TEST_AZURE_BLOB_ACCOUNT_KEY"),
        ServiceUrl: Required("PLIKSHARE_TEST_AZURE_BLOB_SERVICE_URL"));

    public static GoogleCloudStorageCredentials GoogleCloudStorage => new(
        AccessKey: Required("PLIKSHARE_TEST_GCS_ACCESS_KEY"),
        SecretKey: Required("PLIKSHARE_TEST_GCS_SECRET_KEY"));

    /// <summary>
    /// Self-hosted S3-compatible targets. Easiest local setup:
    /// <code>
    /// docker run -d --name plikshare-test-minio -p 9000:9000 minio/minio server /data
    /// docker run -d --name plikshare-test-rustfs -p 9010:9000 rustfs/rustfs
    /// </code>
    /// MinIO listens on <c>http://localhost:9000</c> with <c>minioadmin</c>/<c>minioadmin</c>,
    /// RustFS on <c>http://localhost:9010</c> with <c>rustfsadmin</c>/<c>rustfsadmin</c>.
    /// </summary>
    public static S3CompatibleCredentials S3CompatibleMinio => new(
        AccessKey: Required("PLIKSHARE_TEST_S3_COMPATIBLE_MINIO_ACCESS_KEY"),
        SecretKey: Required("PLIKSHARE_TEST_S3_COMPATIBLE_MINIO_SECRET_KEY"),
        Url: Required("PLIKSHARE_TEST_S3_COMPATIBLE_MINIO_URL"),
        Region: Optional("PLIKSHARE_TEST_S3_COMPATIBLE_MINIO_REGION"),
        ForcePathStyle: OptionalBool("PLIKSHARE_TEST_S3_COMPATIBLE_MINIO_FORCE_PATH_STYLE", defaultValue: true));

    public static S3CompatibleCredentials S3CompatibleRustfs => new(
        AccessKey: Required("PLIKSHARE_TEST_S3_COMPATIBLE_RUSTFS_ACCESS_KEY"),
        SecretKey: Required("PLIKSHARE_TEST_S3_COMPATIBLE_RUSTFS_SECRET_KEY"),
        Url: Required("PLIKSHARE_TEST_S3_COMPATIBLE_RUSTFS_URL"),
        Region: Optional("PLIKSHARE_TEST_S3_COMPATIBLE_RUSTFS_REGION"),
        ForcePathStyle: OptionalBool("PLIKSHARE_TEST_S3_COMPATIBLE_RUSTFS_FORCE_PATH_STYLE", defaultValue: true));

    public static S3CompatibleCredentials S3Compatible(TestStorageBackend backend) => backend switch
    {
        TestStorageBackend.S3CompatibleMinio => S3CompatibleMinio,
        TestStorageBackend.S3CompatibleRustfs => S3CompatibleRustfs,
        _ => throw new ArgumentOutOfRangeException(
            nameof(backend), backend, "Not an S3-compatible test backend.")
    };

    private static string Required(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Required environment variable '{name}' is not set. " +
                "S3 live integration tests need real provider credentials — " +
                "set this variable before running the test.");
        }

        return value;
    }

    private static string? Optional(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool OptionalBool(string name, bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);

        return string.IsNullOrWhiteSpace(value) ? defaultValue : bool.Parse(value);
    }
}

public record AwsS3Credentials(
    string AccessKey,
    string SecretAccessKey,
    string Region);

public record CloudflareR2Credentials(
    string AccessKeyId,
    string SecretAccessKey,
    string Url);

public record BackblazeB2Credentials(
    string KeyId,
    string ApplicationKey,
    string Url);

public record DigitalOceanSpacesCredentials(
    string AccessKey,
    string SecretKey,
    string Region);

public record AzureBlobCredentials(
    string AccountName,
    string AccountKey,
    string ServiceUrl);

public record GoogleCloudStorageCredentials(
    string AccessKey,
    string SecretKey);

public record S3CompatibleCredentials(
    string AccessKey,
    string SecretKey,
    string Url,
    string? Region,
    bool ForcePathStyle);
