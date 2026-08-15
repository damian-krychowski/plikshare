using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Events;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Events;

[McpServerToolType]
public class AckEventsTool
{
    [McpServerTool(Name = AgentToolNames.AckEvents)]
    [Description("Acknowledges your events up to and including the given cursor so they can be cleaned " +
                 "up server-side. Optional — polling already advances your position via the returned " +
                 "cursor; call this only to explicitly confirm you have fully processed everything " +
                 "through a cursor. Pass the 'nextCursor' you most recently processed.")]
    public static async Task<AckEventsResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        AgentEventInbox inbox,
        [Description("Acknowledge all events with a cursor up to and including this value.")]
        string throughCursor,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var agent = await httpContext.GetAgentContext();

        if (!long.TryParse(throughCursor, out var cursor) || cursor < 0)
            throw new McpException("throughCursor must be a non-negative cursor value.");

        var acknowledged = await inbox.Acknowledge(agent.Id, cursor, cancellationToken);

        return new AckEventsResponseDto
        {
            AcknowledgedThrough = throughCursor,
            AcknowledgedCount = acknowledged
        };
    }
}

public class AckEventsResponseDto
{
    public required string AcknowledgedThrough { get; init; }
    public required int AcknowledgedCount { get; init; }
}
