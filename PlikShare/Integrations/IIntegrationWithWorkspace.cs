using PlikShare.Storages.Id;

namespace PlikShare.Integrations;

public interface IIntegrationWithWorkspace
{
    public StorageExtId StorageExternalId { get; }
}