// ReSharper disable InconsistentNaming

using PlikShare.Core.Utils;

namespace PlikShare.Integrations.OpenAi.ChatGpt;

public class ChatGptModel(string alias, FileType[] supportedFileTypes, long maxIncludeSizeInBytes)
{
    public string Alias { get; } = alias;
    public FileType[] SupportedFileTypes { get; } = supportedFileTypes;
    public long MaxIncludeSizeInBytes { get; } = maxIncludeSizeInBytes;

    public static ChatGptModel Gpt4o { get; } = new("gpt-4o", [FileType.Markdown, FileType.Text], SizeInBytes.Mbs(2));
    public static ChatGptModel Gpt4oMini { get; } = new("gpt-4o-mini", [FileType.Markdown, FileType.Text], SizeInBytes.Mbs(2));
    public static ChatGptModel GptO1 { get; } = new("o1", [FileType.Markdown, FileType.Text], SizeInBytes.Mbs(2));
    public static ChatGptModel GptO1Mini { get; } = new("o1-mini", [FileType.Markdown, FileType.Text], SizeInBytes.Mbs(2));
    public static ChatGptModel GptO3Mini { get; } = new("o3-mini", [FileType.Markdown, FileType.Text], SizeInBytes.Mbs(2));

    public static ChatGptModel[] All => [Gpt4o, Gpt4oMini, GptO1, GptO1Mini, GptO3Mini];

    
    public static implicit operator string(ChatGptModel model)
    {
        return model.Alias;
    }

    public static ChatGptModel? FromAlias(string alias)
    {
        return All.FirstOrDefault(model => model.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsSupported(FileType type) => SupportedFileTypes.Contains(type);
}