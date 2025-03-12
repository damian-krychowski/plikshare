using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Integrations.Aws.Textract.Jobs.CheckStatus;
using PlikShare.Integrations.Aws.Textract.Jobs.CheckStatus.Contracts;
using PlikShare.Integrations.Aws.Textract.Jobs.StartJob;
using PlikShare.Integrations.Aws.Textract.Jobs.StartJob.Contracts;
using PlikShare.Integrations.Aws.Textract.TestConfiguration;
using PlikShare.Integrations.Aws.Textract.TestConfiguration.Contracts;
using PlikShare.Workspaces.Validation;

namespace PlikShare.Integrations.Aws.Textract;

public static class TextractEndpoints
{
    public static void MapTextractEndpoints(this WebApplication app)
    {
        var textractConfigGroup = app.MapGroup("/api/integrations/aws-textract")
            .WithTags("Aws Textract Configuration Tests")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Policy = AuthPolicy.Internal,
                Roles = $"{Roles.Admin}"
            })
            .AddEndpointFilter<RequireAppOwnerEndpointFilter>();

        textractConfigGroup.MapGet("/test-image", GetTestImage)
            .WithName("GetTextractTestImage");

        textractConfigGroup.MapPost("/test-configuration", TestTextractConfiguration)
            .WithName("TestTextractConfiguration");

        var textractInWorkspaceGroup = app.MapGroup("/api/workspaces/{workspaceExternalId}/aws-textract")
            .WithTags("Aws Textract for Workspace")
            .RequireAuthorization(policyNames: AuthPolicy.Internal)
            .AddEndpointFilter<ValidateWorkspaceFilter>();

        textractInWorkspaceGroup.MapPost("/jobs", StartTextractJob)
            .WithName("StartTextractJob");

        textractInWorkspaceGroup.MapPost("/jobs/status", CheckTextractJobsStatus)
            .WithName("CheckTextractJobsStatus");
    }

    private static IResult GetTestImage()
    {
        return TypedResults.File(
            TextractTestImage.GetBytes(),
            "image/png");
    }

    private static CheckTextractJobsStatusResponseDto CheckTextractJobsStatus(
        [FromBody] CheckTextractJobsStatusRequestDto request,
        HttpContext httpContext,
        CheckTextractJobsStatusQuery checkTextractJobsStatusQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = checkTextractJobsStatusQuery.Execute(
            workspaceId: workspaceMembership.Workspace.Id,
            request: request);

        return result;
    }

    private static async Task<Results<Ok<TestTextractConfigurationResponseDto>, BadRequest<HttpError>, NotFound<HttpError>>> TestTextractConfiguration(
        [FromBody] TestTextractConfigurationRequestDto request,
        HttpContext httpContext,
        TestTextractConfigurationOperation testTextractConfigurationOperation,
        CancellationToken cancellationToken)
    {
        var result = await testTextractConfigurationOperation.Execute(
            accessKey: request.AccessKey,
            secretAccessKey: request.SecretAccessKey,
            region: request.Region,
            storageExternalId: request.StorageExternalId,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            TestTextractConfigurationOperation.ResultCode.Ok => TypedResults.Ok(
                new TestTextractConfigurationResponseDto
                {
                    Code = TestTextractConfigurationResultCode.Ok,
                    DetectedLines = result.DetectedLines!.Select(l => l.Text).ToList()
                }),

            TestTextractConfigurationOperation.ResultCode.StorageNotFound =>
                HttpErrors.Storage.NotFound(request.StorageExternalId),

            TestTextractConfigurationOperation.ResultCode.AnalysisTimeout =>
                HttpErrors.AwsTextract.AnalysisTimeout(),

            TestTextractConfigurationOperation.ResultCode.TextractAccessDenied =>
                HttpErrors.AwsTextract.AccessDenied(result.ErrorMessage!),

            TestTextractConfigurationOperation.ResultCode.S3AccessDenied =>
                HttpErrors.AwsTextract.S3AccessDenied(result.ErrorMessage!),

            TestTextractConfigurationOperation.ResultCode.TextractInvalidSecretAccessKey =>
                HttpErrors.AwsTextract.InvalidSecretAccessKey(result.ErrorMessage!),

            TestTextractConfigurationOperation.ResultCode.TextractUnrecognizedAccessKey =>
                HttpErrors.AwsTextract.UnrecognizedAccessKey(result.ErrorMessage!),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(TestTextractConfigurationOperation),
                resultValueStr: result.Code.ToString())
        };
    }

    private static async Task<Results<Ok<StartTextractJobResponseDto>, BadRequest<HttpError>>> StartTextractJob(
        [FromBody] StartTextractJobRequestDto request,
        HttpContext httpContext,
        StartTextractJobOperation startTextractJobOperation,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = await startTextractJobOperation.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: request.FileExternalId,
            features: request.Features,
            userIdentity: new UserIdentity(
                UserExternalId: workspaceMembership.User.ExternalId),
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            StartTextractJobOperation.ResultCode.Ok => TypedResults.Ok(new StartTextractJobResponseDto
            {
                ExternalId = result.TextractJob!.ExternalId
            }),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(StartTextractJobOperation),
                resultValueStr: result.Code.ToString())
        };
    }
}