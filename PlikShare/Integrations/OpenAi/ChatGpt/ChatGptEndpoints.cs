using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Core.Utils;
using PlikShare.Integrations.OpenAi.ChatGpt.TestConfiguration;
using PlikShare.Integrations.OpenAi.ChatGpt.TestConfiguration.Contracts;

namespace PlikShare.Integrations.OpenAi.ChatGpt;

public static class ChatGptEndpoints
{
    public static void MapChatGptEndpoints(this WebApplication app)
    {
        var textractConfigGroup = app.MapGroup("/api/integrations/openai-chatgpt")
            .WithTags("OpenAI ChatGTP Configuration Tests")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Policy = AuthPolicy.Internal,
                Roles = $"{Roles.Admin}"
            })
            .AddEndpointFilter<RequireAppOwnerEndpointFilter>();
        
        textractConfigGroup.MapPost("/test-configuration", TestChatGptConfiguration)
            .WithName("TestChatGPTConfiguration");
    }

    private static async Task<Results<Ok<TestChatGptConfigurationResponseDto>, BadRequest<HttpError>, NotFound<HttpError>>> TestChatGptConfiguration(
        [FromBody] TestChatGptConfigurationRequestDto request,
        HttpContext httpContext,
        TestChatGptConfigurationOperation testChatGptConfigurationOperation,
        CancellationToken cancellationToken)
    {
        var result = await testChatGptConfigurationOperation.Execute(
            apiKey: request.ApiKey,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            TestChatGptConfigurationOperation.ResultCode.Ok => 
                TypedResults.Ok(
                    new TestChatGptConfigurationResponseDto
                    {
                        Haiku = result.Haiku!
                    }),

            TestChatGptConfigurationOperation.ResultCode.InvalidApiKey => 
                HttpErrors.OpenAiChatGpt.InvalidApiKey(),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(TestChatGptConfigurationOperation),
                resultValueStr: result.Code.ToString())
        };
    }
}