namespace PlikShare.IntegrationTests.Infrastructure;

public record CapturedEmail(
    IReadOnlyList<string> To,
    string Subject,
    string Html);
