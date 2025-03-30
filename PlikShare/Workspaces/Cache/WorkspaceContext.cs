using PlikShare.Integrations.Aws.Textract;
using PlikShare.Integrations.OpenAi.ChatGpt;
using PlikShare.Storages;
using PlikShare.Users.Cache;
using PlikShare.Workspaces.Id;

namespace PlikShare.Workspaces.Cache;

public sealed class WorkspaceContext
{
    public required int Id { get; init; }
    public required WorkspaceExtId ExternalId { get; init; }
    public required string Name { get; init; }
    public required long CurrentSizeInBytes { get; init; }
    public required long? MaxSizeInBytes { get; init; }
    public required string BucketName { get; init; }
    public required bool IsBucketCreated { get; init; }
    public required bool IsBeingDeleted { get; init; } 
    public required UserContext Owner { get; init; }
    public required IStorageClient Storage { get; init; }
    public required WorkspaceIntegrations Integrations { get; init; }
}

public sealed class WorkspaceIntegrations
{
    public required TextractClient? Textract { get; init; }
    public required List<ChatGptClient> ChatGpt { get; init; }
}
