using System.Net.Http.Headers;
using ModelContextProtocol.Client;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class McpApi(string appUrl)
{
    public async Task<McpAgentSession> ConnectAsAgent(string agentToken)
    {
        var httpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        var httpClient = new HttpClient(httpHandler);
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentToken);

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri($"{appUrl}/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient);

        var client = await McpClient.CreateAsync(transport);

        return new McpAgentSession(
            httpHandler: httpHandler,
            httpClient: httpClient,
            transport: transport,
            client: client);
    }
}
