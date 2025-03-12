using System.Collections.Concurrent;
using PlikShare.Storages.Id;

namespace PlikShare.Storages;

public class StorageClientStore
{
    //todo that could be probably done better rather than having two concurrent dictionaries
    private readonly ConcurrentDictionary<int, IStorageClient> _clientsDict = new ();
    private readonly ConcurrentDictionary<StorageExtId, int> _clientsIdsDict = new();

    public void RegisterClient(IStorageClient client)
    {
        _clientsDict.AddOrUpdate(
            key: client.StorageId,
            addValueFactory: _ => client,
            updateValueFactory: (_, _) => client);

        _clientsIdsDict.AddOrUpdate(
            key: client.ExternalId,
            addValueFactory: _ => client.StorageId,
            updateValueFactory: (_, _) => client.StorageId);
    }
    
    public IStorageClient? TryGetClient(StorageExtId externalId)
    {
        if (!_clientsIdsDict.TryGetValue(externalId, out var storageId)) 
            return null;

        if (TryGetClient(storageId, out var client))
            return client;

        return null;
    }

    public bool TryGetClient(int storageId, out IStorageClient client)
    {
        return _clientsDict.TryGetValue(storageId, out client!);
    }

    public IStorageClient GetClientOrThrow(int storageId)
    {
        if (TryGetClient(storageId, out var client))
            return client;

        throw new InvalidOperationException($"Storage Client for StorageId '{storageId}' was not found");
    }

    public void RemoveClient(int storageId)
    {
        if (_clientsDict.TryRemove(
                key: storageId,
                value: out var client))
        {
            _clientsIdsDict.TryRemove(
                key: client.ExternalId,
                value: out _);
        }
    }
}