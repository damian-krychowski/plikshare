using System.ComponentModel;
using ModelContextProtocol.Server;
using PlikShare.Agents.Events;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Events;

[McpServerToolType]
public class PollEventsTool
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    [McpServerTool(Name = AgentToolNames.PollEvents)]
    [Description("Returns system events that happened in the workspaces and boxes you can access " +
                 "(and system-level events caused by your owner) since your last cursor — any kind of " +
                 "event: files and folders created/renamed/moved/deleted, boxes and share links " +
                 "changed, members invited, settings changed, and so on. Each event is a lightweight " +
                 "signal (type, category, scope, when); use the workspace/box tools to fetch what " +
                 "actually changed. Pass the 'nextCursor' from the previous call as 'afterCursor' to " +
                 "get only newer events; omit it on the first call to start from your oldest pending " +
                 "event. Events you caused yourself are not returned.")]
    public static async Task<PollEventsResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        AgentEventInbox inbox,
        [Description("Return only events newer than this cursor. Pass the 'nextCursor' from the " +
                     "previous poll_events call. Omit on the first call.")]
        string? afterCursor = null,
        [Description("Maximum number of events to return (default 50, max 200).")]
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var agent = await httpContext.GetAgentContext();

        var cursor = ParseCursor(afterCursor);
        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        var events = inbox
            .ListSince(agent.Id, cursor, effectiveLimit)
            .Select(e => new PollEventsResponseDto.Event
            {
                EventId = e.ExternalId,
                EventType = e.EventType,
                Category = e.EventCategory,
                WorkspaceExternalId = e.WorkspaceExternalId,
                BoxExternalId = e.BoxExternalId,
                OccurredAt = e.OccurredAt.ToString("O"),
                Cursor = e.Cursor.ToString()
            })
            .ToList();

        return new PollEventsResponseDto
        {
            Events = events,
            NextCursor = events.Count > 0 ? events[^1].Cursor : (afterCursor ?? "0"),
            HasMore = events.Count == effectiveLimit
        };
    }

    private static long ParseCursor(string? afterCursor)
    {
        if (string.IsNullOrWhiteSpace(afterCursor))
            return 0;

        return long.TryParse(afterCursor, out var value) && value >= 0 ? value : 0;
    }
}

public class PollEventsResponseDto
{
    public required List<Event> Events { get; init; }
    public required string NextCursor { get; init; }
    public required bool HasMore { get; init; }

    public class Event
    {
        public required string EventId { get; init; }
        public required string EventType { get; init; }
        public required string Category { get; init; }
        public string? WorkspaceExternalId { get; init; }
        public string? BoxExternalId { get; init; }
        public required string OccurredAt { get; init; }
        public required string Cursor { get; init; }
    }
}
