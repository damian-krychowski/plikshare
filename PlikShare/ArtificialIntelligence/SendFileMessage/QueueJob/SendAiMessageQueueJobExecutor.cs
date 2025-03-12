using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using CommunityToolkit.HighPerformance;
using OpenAI.Chat;
using PlikShare.ArtificialIntelligence.AiIncludes;
using PlikShare.ArtificialIntelligence.Id;
using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using PlikShare.Integrations.OpenAi.ChatGpt;
using PlikShare.Storages;
using PlikShare.Storages.FileReading;
using Serilog;

namespace PlikShare.ArtificialIntelligence.SendFileMessage.QueueJob;

public class SendAiMessageQueueJobExecutor(
    SaveAiChatCompletionQuery saveAiChatCompletionQuery,
    GetFullAiConversationQuery getFullAiConversationQuery,
    StorageClientStore storageClientStore,
    GetFilesToIncludeDetailsQuery getFilesToIncludeDetailsQuery,
    ChatGptClientStore chatGptClientStore) : IQueueLongRunningJobExecutor
{
    public string JobType => SendAiMessageQueueJobType.Value;
    public int Priority => QueueJobPriority.Normal;

    public async Task<QueueJobResult> Execute(
        string definitionJson, 
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<SendAiMessageQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(SendAiMessageQueueJobDefinition)}'");
        }

        var (getConversationResultCode, conversation) = await getFullAiConversationQuery.GetFullConversation(
            lastMessageExternalId: definition.AiMessageExternalId,
            cancellationToken: cancellationToken);

        if (getConversationResultCode == GetFullAiConversationQuery.ResultCode.NotFound)
        {
            Log.Warning("Could not send AiMessage '{AiMessageExternalId}' because it was not found.",
                definition.AiMessageExternalId);

            return QueueJobResult.Success;
        }

        if (getConversationResultCode == GetFullAiConversationQuery.ResultCode.NewerMessagesFound)
        {
            Log.Warning("AiMessage '{AiMessageExternalId}' was not send because it was not latest one - there is other message that will be processed on the way.",
                definition.AiMessageExternalId);

            return QueueJobResult.Success;
        }

        var lastMessage = conversation!.Messages.Last();

        var chatGptClient = chatGptClientStore.GetClient(
            externalId: conversation.IntegrationExternalId);

        if (chatGptClient is null)
        {
            Log.Warning("Could not send AiMessage '{AiMessageExternalId}' because integration it was sent with '{IntegrationExternalId}' was not found.",
                definition.AiMessageExternalId,
                conversation.IntegrationExternalId);

            return QueueJobResult.Success;
        }

        var chatGptModel = ChatGptModel.FromAlias(
            alias: lastMessage.AiModel);

        if (chatGptModel is null)
        {
            Log.Warning("Could not send AiMessage '{AiMessageExternalId}' because AiModel used to send it '{AiModel}' was not found.",
                definition.AiMessageExternalId,
                lastMessage.AiModel);

            return QueueJobResult.Success;
        }

        var completionTask = GetChatCompletion(
            conversation, 
            chatGptClient, 
            chatGptModel, 
            cancellationToken);

        var newConversationNameTask = GetAiConversationNameIfNeeded(
            conversation,
            lastMessage,
            chatGptClient,
            cancellationToken);

        await Task.WhenAll(
            completionTask,
            newConversationNameTask);

        var completion = await completionTask;
        var newConversationName = await newConversationNameTask;

        Log.Information(
            "ChatCompletion received from AI Integration '{IntegrationExternalId}'. ChatCompletionId: '{ChatCompletionId}', Model: '{ChatCompletionModel}', Token usage: {@TokenUsage}",
            conversation.IntegrationExternalId,
            completion.Id,
            completion.Model,
            completion.Usage);

        var aiCompletion = new AiCompletion
        {
            Id = completion.Id,
            Text = completion.GetWholeText()
        };

        var saveResult = await saveAiChatCompletionQuery.Execute(
            completion: aiCompletion,
            newConversationName: newConversationName,
            conversation: conversation,
            queryMessage: lastMessage,
            cancellationToken: cancellationToken);

        if (saveResult is not null)
        {
            Log.Information(
                "ChatCompletion '{ChatCompletionId}' was converted to new AiMessage '{AiMessageExternalId} ({AiMessageId})' (ConversationCounter: {ConversationCounter}) " +
                "and was successfully added to the AiConversation '{AiConversationExternalId}'",
                completion.Id,
                saveResult.ExternalId,
                saveResult.Id,
                saveResult.ConversationCounter,
                conversation.ExternalId);
        }

        return QueueJobResult.Success;
    }

    private async Task<string?> GetAiConversationNameIfNeeded(
        GetFullAiConversationQuery.AiConversation conversation, 
        GetFullAiConversationQuery.AiMessage lastMessage, 
        ChatGptClient chatGptClient, 
        CancellationToken cancellationToken)
    {
        if (conversation.Name is not null)
            return null;
        
        try
        {
            var titlePrompt = new SystemChatMessage(
                "You are a helpful assistant that creates short, descriptive titles for conversations. " +
                "Generate a concise title (3-7 words) based on the following conversation. " +
                "Return ONLY the title text with no additional formatting, quotes, or explanation.");

            var userMessage = new UserChatMessage(
                lastMessage.Message);
            
            var completion = await chatGptClient.CompleteChatAsync(
                messages: [titlePrompt, userMessage],
                model: chatGptClient.DefaultModel,
                cancellationToken: cancellationToken);

            var title = completion.GetWholeText()
                .Trim()
                .TrimStart('"')
                .TrimEnd('"');

            if (string.IsNullOrWhiteSpace(title))
            {
                return "Untitled Conversation";
            }

            Log.Information(
                "Generated conversation title: '{Title}'. ChatCompletionId: '{ChatCompletionId}', Model: '{ChatCompletionModel}', Token usage: {@TokenUsage}",
                title,
                completion.Id,
                chatGptClient.DefaultModel.Alias,
                completion.Usage);

            return title;
        }
        catch (Exception ex)
        {
            Log.Error(
                ex,
                "Failed to generate conversation title. Using default title instead.");

            return "Untitled Conversation";
        }
    }

    private async Task<ChatCompletion> GetChatCompletion(
        GetFullAiConversationQuery.AiConversation conversation, 
        ChatGptClient chatGptClient,
        ChatGptModel chatGptModel, 
        CancellationToken cancellationToken)
    {
        var includes = conversation
            .Messages
            .SelectMany(m => m.Includes)
            .Distinct()
            .ToList();

        var includeContents = await GetIncludedFiles(
            conversationExternalId: conversation.ExternalId,
            chatGptModel: chatGptModel,
            includes: includes,
            cancellationToken: cancellationToken);

        var systemMessages = includeContents
            .Select(content => new SystemChatMessage(content.Text));

        var chatMessages = conversation
            .Messages
            .Select<GetFullAiConversationQuery.AiMessage, ChatMessage>(m => m.SentByHuman
                ? new UserChatMessage(m.Message)
                : new AssistantChatMessage(m.Message));

        var completion = await chatGptClient.CompleteChatAsync(
            messages:
            [
                new SystemChatMessage(SystemPrompts.FilesFormattingInstruction),
                ..systemMessages,
                ..chatMessages
            ],
            model: chatGptModel,
            cancellationToken: cancellationToken);

        return completion;
    }


    //todo introduce cache
    private async ValueTask<AiIncludeContent[]> GetIncludedFiles(
        AiConversationExtId conversationExternalId,
        ChatGptModel chatGptModel,
        List<AiInclude> includes,
        CancellationToken cancellationToken)
    {
        var fileExternalIds = includes
            .Where(i => i is AiFileInclude)
            .Cast<AiFileInclude>()
            .Select(i => i.ExternalId)
            .Distinct()
            .ToList();

        var filesToInclude = getFilesToIncludeDetailsQuery.GetFilesToInclude(
            externalIds: fileExternalIds);

        var downloadFileTasks = new List<Task<AiIncludeContent>>();

        foreach (var fileToInclude in filesToInclude)
        {
            if (fileToInclude.SizeInBytes > chatGptModel.MaxIncludeSizeInBytes)
            {
                Log.Warning(
                    "File '{FileExternalId}' is too big ({ActualFileSize} > {MaxFileSize}) and will not be included into AiConversation '{AiConversationExternalId}'",
                    fileToInclude.ExternalId,
                    SizeInBytes.AsMb(fileToInclude.SizeInBytes),
                    SizeInBytes.AsMb(chatGptModel.MaxIncludeSizeInBytes),
                    conversationExternalId);

                continue;
            }

            var fileType = ContentTypeHelper.GetFileTypeFromExtension(
                fileToInclude.Extension);

            if (!chatGptModel.IsSupported(fileType))
            {
                Log.Warning(
                    "File '{FileExternalId}' extension '{FileExtension}' is not supported and will not be included into AiConversation '{AiConversationExternalId}'",
                    fileToInclude.ExternalId,
                    fileToInclude.Extension,
                    conversationExternalId);

                continue;
            }


            if (!storageClientStore.TryGetClient(fileToInclude.StorageId, out var storage))
            {
                Log.Warning(
                    "File '{FileExternalId}' Storage#{StorageId} was not found and thus it will not be included into AiConversation '{AiConversationExternalId}'",
                    fileToInclude.ExternalId,
                    fileToInclude.StorageId,
                    conversationExternalId);

                continue;
            }

            var downloadFileTask = DownloadFileContent(
                file: fileToInclude,
                storage: storage,
                cancellationToken: cancellationToken);

            downloadFileTasks.Add(downloadFileTask);
        }

        var results = await Task.WhenAll(
            downloadFileTasks);

        return results;
    }

    private async Task<AiIncludeContent> DownloadFileContent(
        FileToInclude file,
        IStorageClient storage,
        CancellationToken cancellationToken)
    {
        //file has a small size limit so this casting is ok
        var fileSizeInBytes = (int)file.SizeInBytes;

        var heapBuffer = ArrayPool<byte>.Shared.Rent(
            minimumLength: (int) file.SizeInBytes);

        var heapBufferMemory = heapBuffer
            .AsMemory()
            .Slice(0, fileSizeInBytes);

        try
        {
            await FileReader.ReadFull(
                s3FileKey: new S3FileKey
                {
                    S3KeySecretPart = file.S3KeySecretPart,
                    FileExternalId = file.ExternalId
                },
                fileSizeInBytes: file.SizeInBytes,
                bucketName: file.BucketName,
                storage: storage,
                output: PipeWriter.Create(
                    stream: heapBufferMemory.AsStream()),
                cancellationToken: cancellationToken);

            var fileContentBuilder = new StringBuilder();
            var fileBody = Encoding.UTF8.GetString(
                bytes: heapBufferMemory.Span);
            
            var fileName = file.Name + file.Extension;
            var languageIdentifier = ContentTypeHelper.GetMarkdownLanguageIdentifier(
                file.Extension);

            fileContentBuilder.AppendLine($"File: {fileName}");
            fileContentBuilder.AppendLine($"```{languageIdentifier}:{fileName}");
            fileContentBuilder.AppendLine(fileBody);
            fileContentBuilder.AppendLine("```");

            return new AiIncludeContent
            {
                Name = file.Name + file.Extension,
                Text = fileContentBuilder.ToString()
            };
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(
                array: heapBuffer);
        }
    }

    private class AiIncludeContent
    {
        public required string Name { get; init; }
        public required string Text { get; init; }
    }
}
