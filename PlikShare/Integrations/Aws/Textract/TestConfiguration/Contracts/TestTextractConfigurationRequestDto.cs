using PlikShare.Storages.Id;

namespace PlikShare.Integrations.Aws.Textract.TestConfiguration.Contracts;

public class TestTextractConfigurationRequestDto
{
    public required string AccessKey {get;init;}
    public required string SecretAccessKey {get;init;}
    public required string Region { get; init; }
    public required StorageExtId StorageExternalId { get; init; }
}