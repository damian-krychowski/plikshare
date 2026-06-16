using ModelContextProtocol.Client;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public sealed class McpAgentSession : IAsyncDisposable
{
    private readonly HttpClientHandler _httpHandler;
    private readonly HttpClient _httpClient;
    private readonly HttpClientTransport _transport;

    public McpClient Client { get; }

    public McpAgentSession(
        HttpClientHandler httpHandler,
        HttpClient httpClient,
        HttpClientTransport transport,
        McpClient client)
    {
        _httpHandler = httpHandler;
        _httpClient = httpClient;
        _transport = transport;
        Client = client;
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        await _transport.DisposeAsync();
        _httpClient.Dispose();
        _httpHandler.Dispose();
    }
}
