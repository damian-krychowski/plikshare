using ProtoBuf;

namespace PlikShare.QuickShares.Create.Contracts;

[ProtoContract]
public class CreateQuickShareRequestDto
{
    [ProtoMember(1)]
    public required string Name { get; init; }

    [ProtoMember(2)]
    public string? CustomSlug { get; init; }

    [ProtoMember(3)]
    public required List<string> SelectedFiles { get; init; } = [];

    [ProtoMember(4)]
    public required List<string> SelectedFolders { get; init; } = [];

    [ProtoMember(5)]
    public required List<string> ExcludedFiles { get; init; } = [];

    [ProtoMember(6)]
    public required List<string> ExcludedFolders { get; init; } = [];

    [ProtoMember(7)]
    public required string Mode { get; init; }

    [ProtoMember(8)]
    public bool AllowIndividualFileDownload { get; init; }

    [ProtoMember(9)]
    public string? ExpiresAt { get; init; }

    [ProtoMember(10)]
    public string? Password { get; init; }

    [ProtoMember(11)]
    public int? MaxDownloads { get; init; }
}
