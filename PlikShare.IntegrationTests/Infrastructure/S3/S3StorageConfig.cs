namespace PlikShare.IntegrationTests.Infrastructure.S3;

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
