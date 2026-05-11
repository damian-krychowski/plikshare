namespace PlikShare.Users.StorageAccess.Contracts;

public class UpdateUserStorageAccessRequestDto
{
    public required UserStorageAccessMode Mode { get; init; }
    public required List<string> StorageExternalIds { get; init; }
}
