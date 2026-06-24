namespace PlikShare.Mcp.BoxAccess.List.Contracts;

public class ListBoxesResponseDto
{
    public required List<BoxDto> Boxes { get; init; }

    /// <summary>
    /// Set only when no box is shared directly with the agent, to point it at the separate workspace
    /// surface - so an empty result does not read as "no access at all". Omitted (null) otherwise.
    /// </summary>
    public string? Hint { get; init; }

    public class BoxDto
    {
        public required string ExternalId { get; init; }
        public required string Name { get; init; }
        public required bool IsEnabled { get; init; }
        public required string WorkspaceName { get; init; }
    }
}
