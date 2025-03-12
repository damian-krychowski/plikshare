namespace PlikShare.Core.Emails.Definitions;

public record AlertEmailDefinition(
    string Title, 
    string Content, 
    DateTimeOffset EventDateTime);