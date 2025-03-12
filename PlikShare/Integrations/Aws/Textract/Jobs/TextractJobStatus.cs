namespace PlikShare.Integrations.Aws.Textract.Jobs;

public enum TextractJobStatus
{
    WaitsForFile = 0,
    Pending,
    Processing,
    DownloadingResults,
    Completed,
    PartiallyCompleted,
    Failed,
}