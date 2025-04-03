using PlikShare.Core.Utils;
using PlikShare.Integrations.Id;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.Permissions;

namespace PlikShare.Workspaces.Get.Contracts;

public record GetWorkspaceDetailsResponseDto
{
    public required WorkspaceExtId ExternalId { get; init; }
    public required string Name { get; init; }
    public required long CurrentSizeInBytes { get; init; }
    public required long? MaxSizeInBytes { get; init; }
    public required WorkspaceOwnerDto Owner { get; init; }
    public required int PendingUploadsCount { get; init; }
    public required WorkspacePermissions Permissions { get; init; }
    public required WorkspaceIntegrationsDto Integrations { get; init; }
    public required bool IsBucketCreated { get; init; }
}

public class WorkspaceOwnerDto
{
    public required UserExtId ExternalId { get; init; }
    public required string Email { get; init; }
}

public class WorkspaceIntegrationsDto
{
    public required TextractIntegrationDetailsDto? Textract { get; init; }
    public required List<ChatGptIntegrationDetailsDto> ChatGpt { get; init; }
}

public class TextractIntegrationDetailsDto
{
    public required IntegrationExtId ExternalId { get; init; }
    public required string Name { get; init; }
}

public class ChatGptIntegrationDetailsDto
{
    public required IntegrationExtId ExternalId { get; init; }
    public required string Name { get; init; }
    public required List<ChatGptModelDto> Models { get; init; }
    public required string DefaultModel { get; init; }
}

public class ChatGptModelDto
{
    public required string Alias { get; init; }
    public required List<FileType> SupportedFileTypes { get; init; }
    public required long MaxIncludeSizeInBytes { get; init; }
}