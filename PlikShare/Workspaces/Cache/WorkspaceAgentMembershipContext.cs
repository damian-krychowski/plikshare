using PlikShare.Agents.Cache;

namespace PlikShare.Workspaces.Cache;

public record WorkspaceAgentMembershipContext(
    AgentContext Agent,
    WorkspaceContext Workspace,
    bool IsSharedWithAgent)
{
    public bool IsAvailableForAgent =>
        !Workspace.IsBeingDeleted && (IsOwnedByAgent || IsSharedWithAgent);

    public bool IsOwnedByAgent => Workspace.OwnerAgent?.Id == Agent.Id;
}
