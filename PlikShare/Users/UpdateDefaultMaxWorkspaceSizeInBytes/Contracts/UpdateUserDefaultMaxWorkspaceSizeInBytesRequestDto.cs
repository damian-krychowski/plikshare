namespace PlikShare.Users.UpdateDefaultMaxWorkspaceSizeInBytes.Contracts;

public class UpdateUserDefaultMaxWorkspaceSizeInBytesRequestDto
{
    public required long? DefaultMaxWorkspaceSizeInBytes { get; init; }
}