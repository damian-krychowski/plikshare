using PlikShare.Agents.Id;

namespace PlikShare.Core.UserIdentity;

public record AgentIdentity(AgentExtId AgentExternalId) : IUserIdentity
{
    public const string Type = "agent_external_id";
    public string IdentityType => Type;
    public string Identity => AgentExternalId.Value;
}
