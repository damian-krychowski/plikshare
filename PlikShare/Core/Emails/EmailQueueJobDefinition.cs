namespace PlikShare.Core.Emails;

public class EmailQueueJobDefinition<TEmailDefinition>
{
    public required string Email { get; init; }
    public required EmailTemplate Template { get; init; }
    public required TEmailDefinition Definition { get; init; }
}