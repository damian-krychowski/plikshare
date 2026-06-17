namespace PlikShare.Mcp;

/// <summary>
/// Uniform envelope every agent tool returns. <c>executed</c> carries the tool's own result;
/// <c>waits_for_approval</c> carries the operation id to poll/commit; <c>rejected</c> carries the
/// reason. Null fields are omitted by MCP serialization, so each status yields a clean shape.
/// </summary>
public class AgentToolResponse
{
    public required string Status { get; init; }

    public object? Result { get; init; }

    public string? ApprovalRequestId { get; init; }
    public string? ExpiresAt { get; init; }

    public string? Reason { get; init; }
    public string? Message { get; init; }

    public static AgentToolResponse Executed(object? result) => new()
    {
        Status = "executed",
        Result = result
    };

    private const string WaitsForApprovalMessage =
        "This operation needs human approval. Ask the user to approve it in PlikShare under Agent " +
        "requests, poll check_approvals to watch its status, and once it is 'approved' call " +
        "execute_operation with its approvalRequestId to run it.";

    public static AgentToolResponse WaitsForApproval(
        string approvalRequestId,
        DateTimeOffset expiresAt) => new()
    {
        Status = "waits_for_approval",
        ApprovalRequestId = approvalRequestId,
        ExpiresAt = expiresAt.ToString("O"),
        Message = WaitsForApprovalMessage
    };

    public static AgentToolResponse Rejected(string reason, string message) => new()
    {
        Status = "rejected",
        Reason = reason,
        Message = message
    };
}
