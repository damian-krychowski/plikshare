using System.Text.Json;
using PlikShare.Agents.Id;
using PlikShare.Agents.Operations.Id;
using PlikShare.Workspaces.Id;

namespace PlikShare.Agents.Operations.List.Contracts;

public class GetPendingAgentOperationsResponseDto
{
    public required List<Item> Items { get; init; }

    public class Item
    {
        public required AgentOperationExtId ExternalId { get; init; }
        public required string ToolName { get; init; }
        public required JsonElement Parameters { get; init; }
        public required Agent Agent { get; init; }
        public required Workspace? Workspace { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
    }

    public class Agent
    {
        public required AgentExtId ExternalId { get; init; }
        public required string Name { get; init; }
    }

    public class Workspace
    {
        public required WorkspaceExtId ExternalId { get; init; }
        public required string Name { get; init; }
    }
}
