using Amazon.Textract;
using Amazon.Textract.Model;
using PlikShare.Core.Utils;
using PlikShare.Storages;
using Serilog;

namespace PlikShare.Integrations.Aws.Textract;

public class AwsTextractClient(
    string accessKey,
    string secretAccessKey,
    string region)
{
    private readonly RateLimiter _rateLimiter = new(100, 100);

    private readonly IAmazonTextract _textractClient = new AmazonTextractClient(
        accessKey, 
        secretAccessKey,
        Amazon.RegionEndpoint.GetBySystemName(region));
    
    public async Task<StartDocumentAnalysisResponse> InitiateAnalysis(
        S3FileKey fileKey,
        string bucketName,
        TextractFeature[] features,
        CancellationToken cancellationToken)
    {
        var featureList = features.ToAwsFormat();

        try
        {
            using var permission = await _rateLimiter.AcquirePermission(
                cancellationToken: cancellationToken);

            var result = await _textractClient.StartDocumentAnalysisAsync(
                request: new StartDocumentAnalysisRequest
                {
                    FeatureTypes = featureList,
                    DocumentLocation = new DocumentLocation
                    {
                        S3Object = new S3Object
                        {
                            Name = fileKey.Value,
                            Bucket = bucketName
                        }
                    }
                },
                cancellationToken: cancellationToken);
            
            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex,
                "Failed to initiate document analysis for file '{FileKey}' in bucket '{BucketName}' with features '{Features}' in region '{Region}'",
                fileKey.Value, 
                bucketName, 
                string.Join(", ", featureList), 
                region);

            throw;
        }
    }

    public async Task<GetDocumentAnalysisResponse> GetAnalysisResult(
        string analysisJobId,
        string? nextToken,
        CancellationToken cancellationToken)
    {
        try
        {
            using var permission = await _rateLimiter.AcquirePermission(
                cancellationToken: cancellationToken);

            var result = await _textractClient.GetDocumentAnalysisAsync(
                request: new GetDocumentAnalysisRequest
                {
                    JobId = analysisJobId,
                    NextToken = nextToken
                },
                cancellationToken: cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex,
                "Failed to get document analysis result for job '{JobId}' with nextToken '{NextToken}' in region '{Region}'",
                analysisJobId,
                nextToken,
                region);

            throw;
        }
    }
}