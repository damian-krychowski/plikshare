using PlikShare.Agents.Cache;
using PlikShare.Core.Authorization;

namespace PlikShare.Agents.Middleware;

public static class AgentHttpContextExtensions
{
    private const string AgentContextKey = "agent-context";

    public static async ValueTask<AgentContext> GetAgentContext(
        this HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(AgentContextKey, out var cached)
            && cached is AgentContext agentContext)
        {
            return agentContext;
        }

        var externalId = httpContext.User.TryGetAgentExternalId()
            ?? throw new InvalidOperationException(
                "'AgentExternalId' claim was not found on the current principal.");

        var agentCache = httpContext
            .RequestServices
            .GetRequiredService<AgentCache>();

        var context = await agentCache.GetOrThrow(
            externalId,
            httpContext.RequestAborted);

        httpContext.Items[AgentContextKey] = context;

        return context;
    }
}
