using PlikShare.Integrations.Id;
using PlikShare.Workspaces.Id;

namespace PlikShare.Integrations.List.Contracts;

public class GetIntegrationsResponseDto
{
    public required List<GetIntegrationsItemResponseDto> Items { get; init; }
}

public class GetIntegrationsItemResponseDto
{
    public required string Name { get; init; }
    public required IntegrationExtId ExternalId { get; init; }
    public required IntegrationType Type { get; init; }
    public required bool IsActive { get; init; }
    public required IntegrationWorkspaceDto Workspace { get; init; }
}

public class IntegrationWorkspaceDto
{
    public required WorkspaceExtId ExternalId { get; init; }
    public required string Name { get; init; }
}