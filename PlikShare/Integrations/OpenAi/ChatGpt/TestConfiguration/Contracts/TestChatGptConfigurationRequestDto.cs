namespace PlikShare.Integrations.OpenAi.ChatGpt.TestConfiguration.Contracts;

public class TestChatGptConfigurationRequestDto
{
    public required string ApiKey { get; init; }
}

public class TestChatGptConfigurationResponseDto
{
    public required string Haiku { get; init; }
}