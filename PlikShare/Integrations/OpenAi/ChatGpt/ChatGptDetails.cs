using PlikShare.Storages.Id;

namespace PlikShare.Integrations.OpenAi.ChatGpt;

public class ChatGptDetails : IIntegrationWithWorkspace
{
    public required string ApiKey { get; init; }
    public required StorageExtId StorageExternalId { get; init; }
}