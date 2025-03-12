using System.Text;
using OpenAI.Chat;

namespace PlikShare.Integrations.OpenAi.ChatGpt.TestConfiguration;

public class TestChatGptConfigurationOperation
{
    public async Task<Result> Execute(
        string apiKey,
        CancellationToken cancellationToken)
    {
        var client = new ChatClient(
            model: ChatGptModel.Gpt4oMini,
            apiKey: apiKey);

        try
        {
            var response = await client.CompleteChatAsync(
                messages:
                [
                    new UserChatMessage("Generate a haiku about Plikshare file sharing software. The haiku should highlight its features or benefits.")
                ],
                cancellationToken: cancellationToken);

            var result = new StringBuilder();

            foreach (var chatMessageContentPart in response.Value.Content)
            {
                result.AppendLine(chatMessageContentPart.Text);
            }

            return new Result
            {
                Code = ResultCode.Ok,
                Haiku = result.ToString()
            };
        }
        catch (UnauthorizedAccessException)
        {
            return new Result
            {
                Code = ResultCode.InvalidApiKey
            };
        }
    }

    public enum ResultCode
    {
        Ok,
        InvalidApiKey
    }

    public class Result
    {
        public required ResultCode Code { get; init; }
        public string? Haiku { get; init; }
    }
}