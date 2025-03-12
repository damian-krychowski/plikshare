using Amazon.Textract.Model;
using PlikShare.Integrations.Id;
using PlikShare.Storages;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Integrations.Aws.Textract;

public class TextractClient(
    WorkspaceCache workspaceCache,
    AwsTextractClient awsClient, 
    int integrationId,
    int storageId,
    int workspaceId,
    IntegrationExtId externalId,
    string name)
{
    public int IntegrationId { get; } = integrationId;
    public int StorageId { get; } = storageId;
    public int WorkspaceId { get; } = workspaceId;
    public IntegrationExtId ExternalId { get; } = externalId;
    public string Name { get; } = name;

    public async Task<StartDocumentAnalysisResponse> InitiateAnalysis(
        S3FileKey fileKey,
        int fileWorkspaceId,
        TextractFeature[] features,
        CancellationToken cancellationToken)
    {
        var workspace = await workspaceCache.TryGetWorkspace(
            fileWorkspaceId,
            cancellationToken);

        if (workspace is null)
            throw new InvalidOperationException(
                $"Cannot start Textract document analysis of file '{fileKey.FileExternalId}' because workspace#{fileWorkspaceId} was not found");

        return await awsClient.InitiateAnalysis(
            fileKey: fileKey,
            bucketName: workspace.BucketName,
            features: features,
            cancellationToken: cancellationToken);
    }

    public async Task<GetDocumentAnalysisResponse> GetAnalysisResult(
        string analysisJobId,
        string? nextToken,
        CancellationToken cancellationToken)
    {
        return await awsClient.GetAnalysisResult(
            analysisJobId: analysisJobId,
            nextToken: nextToken,
            cancellationToken: cancellationToken);
    }
}