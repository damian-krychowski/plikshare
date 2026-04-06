using ProtoBuf;

namespace PlikShare.AuditLog.Contracts;

[ProtoContract]
public class GetAuditLogResponseDto
{
    [ProtoMember(1)]
    public required List<AuditLogItemDto> Items { get; init; }

    [ProtoMember(2)]
    public int? NextCursor { get; init; }

    [ProtoMember(3)]
    public required bool HasMore { get; init; }
}

[ProtoContract]
public class AuditLogItemDto
{
    [ProtoMember(1)]
    public required string ExternalId { get; init; }

    [ProtoMember(2)]
    public required string CreatedAt { get; init; }

    [ProtoMember(3)]
    public string? ActorEmail { get; init; }

    [ProtoMember(4)]
    public required string ActorIdentity { get; init; }

    [ProtoMember(5)]
    public required string EventType { get; init; }

    [ProtoMember(6)]
    public required string EventSeverity { get; init; }
}
