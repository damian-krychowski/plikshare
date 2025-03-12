namespace PlikShare.Integrations;

public enum IntegrationType
{
    AwsTextract,
    OpenaiChatgpt
}

public static class IntegrationTypeExtensions
{
    public static string GetWorkspaceNamePrefix(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.AwsTextract => "Textract",
            IntegrationType.OpenaiChatgpt => "ChatGpt",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}