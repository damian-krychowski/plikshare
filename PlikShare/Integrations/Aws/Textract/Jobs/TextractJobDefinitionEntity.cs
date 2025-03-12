namespace PlikShare.Integrations.Aws.Textract.Jobs;

public class TextractJobDefinitionEntity
{
    public required TextractFeature[] Features { get; init; }
}