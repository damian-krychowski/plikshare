namespace PlikShare.Core.Emails.Definitions;

public class AlertEmailDefinition
{
    public required string Title { get; init; }
    public required string Content { get; init; }
    public required DateTimeOffset EventDateTime { get; init; }
}