using PlikShare.Files.Id;

namespace PlikShare.Integrations.Aws.Textract.Jobs.StartJob.Contracts;

public class StartTextractJobRequestDto
{
    public required FileExtId FileExternalId { get; init; }
    public required TextractFeature[] Features { get; init; }
}