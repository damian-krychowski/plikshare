namespace PlikShare.Mcp.Storages.List.Contracts;

public class ListStoragesResponseDto
{
    public required List<StorageItemDto> Storages { get; init; }
}

public class StorageItemDto
{
    public required string StorageExternalId { get; init; }
    public required string Name { get; init; }
    public required string EncryptionType { get; init; }
}
