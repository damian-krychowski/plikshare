using System.Diagnostics;
using System.Text;
using OpenAI;
using OpenAI.Chat;
using PlikShare.Integrations.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Integrations.OpenAi.ChatGpt;

public class ChatGptClient(
    WorkspaceCache workspaceCache,
    string apiKey,
    int integrationId,
    int storageId,
    int workspaceId,
    IntegrationExtId externalId,
    string name)
{
    private readonly WorkspaceCache _workspaceCache = workspaceCache;
    private readonly string _apiKey = apiKey;
    private readonly OpenAIClient _client = new(apiKey);

    public int IntegrationId { get; } = integrationId;
    public int StorageId { get; } = storageId;
    public int WorkspaceId { get; } = workspaceId;
    public IntegrationExtId ExternalId { get; } = externalId;
    public string Name { get; } = name;
    public ChatGptModel[] Models { get; } = ChatGptModel.All;
    public ChatGptModel DefaultModel => ChatGptModel.Gpt4oMini;
    
    public async Task<ChatCompletion> CompleteChatAsync(
        List<ChatMessage> messages,
        ChatGptModel model,
        CancellationToken cancellationToken)
    {
        // Create a stopwatch to measure the API call duration
        var stopwatch = Stopwatch.StartNew();
        var messageCount = messages.Count;

        try
        {
            var chatClient = _client.GetChatClient(
                model.Alias);

            var result = await chatClient.CompleteChatAsync(
                messages: messages,
                cancellationToken: cancellationToken);

            stopwatch.Stop();

            var totalTokenCount = result.Value.Usage.TotalTokenCount;

            Log.Information(
                "ChatGpt API call completed successfully in {ElapsedMs}ms for integration '{IntegrationExternalId}'. " +
                "Model: '{ChatGptModel}', Messages: {MessageCount}, Total tokens: {TotalTokens}, " +
                "Prompt tokens: {PromptTokens}, Completion tokens: {CompletionTokens}, " +
                "Tokens per second: {TokensPerSecond:F2}",
                stopwatch.ElapsedMilliseconds,
                ExternalId,
                model,
                messageCount,
                totalTokenCount,
                result.Value.Usage.InputTokenCount,
                result.Value.Usage.OutputTokenCount,
                totalTokenCount > 0 ? (totalTokenCount / (stopwatch.ElapsedMilliseconds / 1000.0)) : 0);

            return result.Value;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            Log.Error(ex,
                "ChatGpt API call failed after {ElapsedMs}ms for integration '{IntegrationExternalId}' " +
                "using model '{ChatGptModel}' with {MessageCount} messages",
                stopwatch.ElapsedMilliseconds,
                ExternalId,
                model,
                messageCount);

            throw;
        }
    }
}

public static class ChatCompletionExtensions
{
    public static string GetWholeText(this ChatCompletion completion)
    {
        var builder = completion
            .Content
            .Aggregate(new StringBuilder(), (acc, part) =>
            {
                if (part.Kind != ChatMessageContentPartKind.Text)
                {
                    Log.Warning("AI part {@Part} responded with kind {AiResponseKind}",
                        part,
                        part.Kind);
                }

                acc.Append(part.Text);

                return acc;
            });

        return builder.ToString();
    }
}