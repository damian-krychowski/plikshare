using ProtoBuf;

namespace PlikShare.QuickShares.Create.Contracts;

[ProtoContract]
public class CreateQuickShareResponseDto
{
    [ProtoMember(1)]
    public required string ExternalId { get; init; }

    [ProtoMember(2)]
    public required string Slug { get; init; }

    [ProtoMember(3)]
    public required string Url { get; init; }
}
