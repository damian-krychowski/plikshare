namespace PlikShare.Integrations.Aws.Textract;

public class TextractClientStore
{
    private readonly List<TextractClient> _clients = new();

    public void RegisterClient(TextractClient client)
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

    public TextractClient? TryGetClient(int integrationId)
    {
        return _clients.FirstOrDefault(c => c.IntegrationId == integrationId);
    }

    //to improve this function i could also check if storage is aws with the same textract region
    //but for now i will simply check if there is a textract with the same storageId - if not, I return the first one from the list
    public TextractClient? TryGetClient(int workspaceId, int storageId)
    {
        var matchingByWorkspace = _clients
            .FirstOrDefault(c => c.WorkspaceId == workspaceId);

        if (matchingByWorkspace != null)
        {
            return matchingByWorkspace;
        }

        var matchingClient = _clients
            .FirstOrDefault(c => c.StorageId == storageId);

        if(matchingClient != null) {
            return matchingClient;
        }

        var anyClient = _clients.FirstOrDefault();

        if (anyClient != null)
        {
            return anyClient;
        }

        return null;
    }
}