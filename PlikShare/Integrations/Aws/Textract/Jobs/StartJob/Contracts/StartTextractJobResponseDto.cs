using PlikShare.Integrations.Aws.Textract.Id;

namespace PlikShare.Integrations.Aws.Textract.Jobs.StartJob.Contracts;

public class StartTextractJobResponseDto
{
    public required TextractJobExtId ExternalId { get; init; }
}