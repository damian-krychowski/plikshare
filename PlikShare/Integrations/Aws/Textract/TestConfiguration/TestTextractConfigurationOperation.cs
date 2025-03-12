using Amazon.Textract;
using Amazon.Textract.Model;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Files.Delete.QueueJob;
using PlikShare.Files.Records;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Storages.S3;
using PlikShare.Storages.S3.Upload;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Cache;
using PlikShare.Workspaces.DeleteBucket;
using Serilog;

namespace PlikShare.Integrations.Aws.Textract.TestConfiguration;

public class TestTextractConfigurationOperation(
    StorageClientStore storageClientStore,
    DbWriteQueue dbWriteQueue,
    IQueue queue,
    IClock clock)
{
    public async Task<Result> Execute(
        string accessKey,
        string secretAccessKey,
        string region,
        StorageExtId storageExternalId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var storageClient = storageClientStore.TryGetClient(
                externalId: storageExternalId);

            if (storageClient is not S3StorageClient s3StorageClient)
                return new Result { Code = ResultCode.StorageNotFound };

            var bucketName = $"textract-test-{Guid.NewGuid()}";

            await storageClient.CreateBucketIfDoesntExist(
                bucketName: bucketName,
                cancellationToken: cancellationToken);

            var imageFileKey = S3FileKey.NewKey();

            await UploadTestImageToS3(
                imageFileKey, 
                bucketName, 
                s3StorageClient, 
                cancellationToken);

            var result = await TestTextract(
                accessKey, 
                secretAccessKey, 
                region, 
                imageFileKey, 
                bucketName, 
                cancellationToken);

            await ScheduleDeleteTaskAndBucket(
                bucketName: bucketName,
                fileKey: imageFileKey,
                storageId: storageClient.StorageId,
                correlationId: correlationId,
                cancellationToken: cancellationToken);
            
            return result;
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while preparing setup for AwsTextract integration (AccessKey: '{AccessKey}')", accessKey);

            throw;
        }
    }

    private static async Task<Result> TestTextract(
        string accessKey, 
        string secretAccessKey, 
        string region,
        S3FileKey imageFileKey, 
        string bucketName, 
        CancellationToken cancellationToken)
    {
        try
        {
            var client = new AwsTextractClient(
                accessKey: accessKey,
                secretAccessKey: secretAccessKey,
                region: region);

            var analysisJob = await client.InitiateAnalysis(
                fileKey: imageFileKey,
                bucketName: bucketName,
                features: [TextractFeature.Layout],
                cancellationToken);

            var result = await WaitForAnalysisResult(
                analysisJob: analysisJob,
                client: client,
                cancellationToken: cancellationToken);

            return result;

        }
        catch (InvalidS3ObjectException e)
        {
            Log.Warning(e, "S3 access denied for AwsTextract integration (AccessKey: '{AccessKey}')", accessKey);

            return new Result
            {
                Code = ResultCode.S3AccessDenied,
                ErrorMessage = e.Message
            };
        }
        catch (AccessDeniedException e)
        {
            Log.Warning(e, "Textract access denied for AwsTextract integration (AccessKey: '{AccessKey}')", accessKey);

            return new Result
            {
                Code = ResultCode.TextractAccessDenied,
                ErrorMessage = e.Message
            };
        }
        catch (AmazonTextractException e)
        {
            if (e.ErrorCode == "InvalidSignatureException")
            {
                Log.Warning(e, "Textract secret access key is invalid for AwsTextract integration (AccessKey: '{AccessKey}')", accessKey);

                return new Result
                {
                    Code = ResultCode.TextractInvalidSecretAccessKey,
                    ErrorMessage = e.Message
                };
            }

            if (e.ErrorCode == "UnrecognizedClientException")
            {
                Log.Warning(e, "Textract access key is not recognized for AwsTextract integration (AccessKey: '{AccessKey}')", accessKey);

                return new Result
                {
                    Code = ResultCode.TextractUnrecognizedAccessKey,
                    ErrorMessage = e.Message
                };
            }

            Log.Error(e, "Something went wrong while preparing setup for AwsTextract integration (AccessKey: '{AccessKey}')", accessKey);

            return new Result
            {
                Code = ResultCode.SomethingWentWrong
            };
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while preparing setup for AwsTextract integration (AccessKey: '{AccessKey}')", accessKey);

            return new Result
            {
                Code = ResultCode.SomethingWentWrong
            };
        }
    }

    private static async Task<Result> WaitForAnalysisResult(
        StartDocumentAnalysisResponse analysisJob,
        AwsTextractClient client,
        CancellationToken cancellationToken)
    {
        var count = 20;

        while (count > 0)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(2),
                cancellationToken);

            var analysisResult = await client.GetAnalysisResult(
                analysisJobId: analysisJob.JobId,
            nextToken: null,
                cancellationToken: cancellationToken);

            if (analysisResult.JobStatus == JobStatus.SUCCEEDED)
            {
                return new Result
                {
                    Code = ResultCode.Ok,
                    DetectedLines = analysisResult
                        .Blocks
                        .Where(b => b.BlockType == BlockType.LINE)
                        .Select(b => new DetectedLine
                        {
                            Text = b.Text
                        })
                        .ToArray()
                };
            }

            count--;
        }

        return new Result { Code = ResultCode.AnalysisTimeout };
    }

    private static async Task UploadTestImageToS3(
        S3FileKey imageFileKey,
        string bucketName, 
        S3StorageClient s3StorageClient, 
        CancellationToken cancellationToken)
    {
        var imageBytes = TextractTestImage.GetBytes();

        var uploadResult = await S3UploadOperation.Execute(
            fileBytes: imageBytes.AsMemory(),
            file: new FileToUploadDetails
            {
                SizeInBytes = imageBytes.Length,
                Encryption = new FileEncryption { EncryptionType = StorageEncryptionType.None },
                S3FileKey = imageFileKey,
                S3UploadId = String.Empty
            },
            part: new FilePartDetails
            {
                SizeInBytes = imageBytes.Length,
                Number = 1,
                UploadAlgorithm = UploadAlgorithm.DirectUpload
            },
            bucketName: bucketName,
            s3StorageClient: s3StorageClient,
            cancellationToken: cancellationToken);
    }

    private Task ScheduleDeleteTaskAndBucket(
        string bucketName,
        S3FileKey fileKey,
        int storageId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context =>
            {
                using var transaction = context.Connection.BeginTransaction();

                try
                {
                    var sagaId = queue.InsertSaga(
                        correlationId: correlationId,
                        onCompletedJobType: DeleteBucketQueueJobType.Value,
                        onCompletedJobDefinition: new DeleteBucketQueueJobDefinition
                        {
                            BucketName = bucketName,
                            StorageId = storageId
                        },
                        dbWriteContext: context,
                        transaction: transaction);

                    var queueJobId = queue.EnqueueOrThrow(
                        correlationId: correlationId,
                        jobType: DeleteS3FileQueueJobType.Value,
                        definition: new DeleteS3FileQueueJobDefinition
                        {
                            StorageId = storageId,
                            BucketName = bucketName,
                            FileExternalId = fileKey.FileExternalId,
                            S3KeySecretPart = fileKey.S3KeySecretPart
                        },
                        executeAfterDate: clock.UtcNow,
                        debounceId: null,
                        sagaId: sagaId,
                        dbWriteContext: context,
                        transaction: transaction);

                    transaction.Commit();
                }
                catch (Exception e)
                {
                    transaction.Rollback();

                    Log.Error(e, "Something went wrong while scheduling file and bucket cleanup after textract integration test.");
                    throw;
                }
            },
            cancellationToken: cancellationToken);
    }



    public enum ResultCode
    {
        Ok = 0,
        StorageNotFound,
        AnalysisTimeout,
        TextractAccessDenied,
        S3AccessDenied,
        SomethingWentWrong,
        TextractInvalidSecretAccessKey,
        TextractUnrecognizedAccessKey
    }

    public class DetectedLine
    {
        public required string Text { get; init; }
    }

    public class Result
    {
        public required ResultCode Code { get; init; }
        public DetectedLine[]? DetectedLines { get; init; }
        public string? ErrorMessage { get; init; }
    }
}