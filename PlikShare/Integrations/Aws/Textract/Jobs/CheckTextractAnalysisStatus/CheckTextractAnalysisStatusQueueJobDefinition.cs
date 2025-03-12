namespace PlikShare.Integrations.Aws.Textract.Jobs.CheckTextractAnalysisStatus;

public class CheckTextractAnalysisStatusQueueJobDefinition
{
    public required int TextractJobId { get; init; }
}