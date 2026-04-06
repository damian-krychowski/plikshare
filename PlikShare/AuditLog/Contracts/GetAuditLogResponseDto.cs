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
    public string? CorrelationId { get; init; }

    [ProtoMember(4)]
    public required string ActorIdentityType { get; init; }

    [ProtoMember(5)]
    public required string ActorIdentity { get; init; }

    [ProtoMember(6)]
    public string? ActorEmail { get; init; }

    [ProtoMember(7)]
    public string? ActorIp { get; init; }

    [ProtoMember(8)]
    public required string EventCategory { get; init; }

    [ProtoMember(9)]
    public required string EventType { get; init; }

    [ProtoMember(10)]
    public required string EventSeverity { get; init; }

    [ProtoMember(11)]
    public string? WorkspaceExternalId { get; init; }

    [ProtoMember(12)]
    public string? Details { get; init; }
}
