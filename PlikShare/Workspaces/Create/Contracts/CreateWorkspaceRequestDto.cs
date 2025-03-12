using PlikShare.Storages.Id;

namespace PlikShare.Workspaces.Create.Contracts;

public record CreateWorkspaceRequestDto(
    StorageExtId StorageExternalId, 
    string Name);