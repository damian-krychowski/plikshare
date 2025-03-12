namespace PlikShare.Integrations.Aws.Textract.TestConfiguration.Contracts;

public class TestTextractConfigurationResponseDto
{
    public required TestTextractConfigurationResultCode Code { get; init; }
    public required List<string> DetectedLines { get; init; }
}

public enum TestTextractConfigurationResultCode
{
    Ok
}