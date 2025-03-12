using PlikShare.Integrations.Aws.Textract.Id;

namespace PlikShare.Integrations.Aws.Textract.Jobs.CheckStatus.Contracts;

public class CheckTextractJobsStatusRequestDto
{
    public required List<TextractJobExtId> ExternalIds { get; init; }
}

public class CheckTextractJobsStatusResponseDto
{
    public required List<TextractJobStatusItemDto> Items { get; init; }
}

public class TextractJobStatusItemDto
{
    public required TextractJobExtId ExternalId { get; init; }
    public required TextractJobStatus Status { get; init; }
}