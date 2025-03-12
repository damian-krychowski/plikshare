using PlikShare.Integrations.Id;

namespace PlikShare.Integrations.OpenAi.ChatGpt;

public class ChatGptClientStore
{
    private readonly List<ChatGptClient> _clients = new();

    public void RegisterClient(ChatGptClient client)
    {   
        _clients.Add(client);
    }

    public void RemoveClient(int integrationId)
    {
        var client = _clients
            .FirstOrDefault(c => c.IntegrationId == integrationId);

        if (client is not null)
        {
            _clients.Remove(client);
        }
    }
    
    public List<ChatGptClient> GetClients()
    {
        return _clients.ToList();
    }

    public ChatGptClient? GetClient(IntegrationExtId externalId)
    {
        return _clients.FirstOrDefault(x => x.ExternalId == externalId);
    }
}