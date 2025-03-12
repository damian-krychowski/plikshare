using PlikShare.Storages.Id;

namespace PlikShare.Integrations.Aws.Textract;

public class AwsTextractDetails: IIntegrationWithWorkspace
{
    public required string AccessKey { get; init; }
    public required string SecretAccessKey { get; init; }
    public required string Region { get; init; }
    public required StorageExtId StorageExternalId { get; init; }
}