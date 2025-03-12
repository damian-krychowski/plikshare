using PlikShare.Integrations.Id;

namespace PlikShare.Core.UserIdentity;

/// <summary>
/// This class represents situation where given entity/message/file/etc was created by app integration rather than user
/// For example, ai message created by response from AI integration, like ChatGpt
/// </summary>
/// <param name="IntegrationExternalId"></param>
public record IntegrationUserIdentity(IntegrationExtId IntegrationExternalId) : IUserIdentity
{
    public const string Type = "integration_external_id";

    public string IdentityType => Type;
    public string Identity => IntegrationExternalId.Value;
}