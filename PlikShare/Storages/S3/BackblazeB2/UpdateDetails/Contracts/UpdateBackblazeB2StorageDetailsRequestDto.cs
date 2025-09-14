namespace PlikShare.Storages.S3.BackblazeB2.UpdateDetails.Contracts;

public class UpdateBackblazeB2StorageDetailsRequestDto
{
    public required string KeyId { get; init; }
    public required string ApplicationKey { get; init; }
    public required string Url { get; init; }
}