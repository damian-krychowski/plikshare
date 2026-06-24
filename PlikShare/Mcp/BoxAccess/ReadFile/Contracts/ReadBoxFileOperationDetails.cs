using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.BoxAccess.ReadFile.Contracts;

/// <summary>
/// read_box_file reads the text content of a file inside a box; its details carry the box (id and name),
/// the file's name and path and the byte range it would read, so a human reviewing the approval sees
/// exactly which file the agent wants to read.
/// </summary>
public class ReadBoxFileOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.ReadBoxFile;

    public required string BoxExternalId { get; init; }
    public required string? BoxName { get; init; }
    public required string FileExternalId { get; init; }
    public required string? Name { get; init; }
    public required string? Path { get; init; }
    public required long Offset { get; init; }
    public required int? MaxBytes { get; init; }
}
