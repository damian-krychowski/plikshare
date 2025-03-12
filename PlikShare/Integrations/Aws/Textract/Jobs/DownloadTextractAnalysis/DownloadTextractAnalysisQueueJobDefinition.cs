namespace PlikShare.Integrations.Aws.Textract.Jobs.DownloadTextractAnalysis;

public class DownloadTextractAnalysisQueueJobDefinition
{
    public required int TextractJobId { get; init; }
    public required Guid TextractTemporaryStoreId { get; init; }
}