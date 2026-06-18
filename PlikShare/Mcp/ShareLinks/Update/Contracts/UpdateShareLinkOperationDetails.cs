using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.ShareLinks.Update.Contracts;

public class UpdateShareLinkOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.UpdateShareLink;

    public required string ShareLinkExternalId { get; init; }
    public required string? CurrentName { get; init; }

    public required bool UpdateName { get; init; }
    public required string? NewName { get; init; }

    public required bool UpdateExpiration { get; init; }
    public required string? ExpiresAt { get; init; }

    public required bool UpdateMaxDownloads { get; init; }
    public required int? MaxDownloads { get; init; }

    public required bool UpdatePassword { get; init; }
    public required bool PasswordSet { get; init; }
}
