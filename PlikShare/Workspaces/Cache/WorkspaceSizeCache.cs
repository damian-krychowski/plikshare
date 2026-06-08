using PlikShare.Workspaces.GetSize;
using System.Collections.Concurrent;

namespace PlikShare.Workspaces.Cache;

public sealed class WorkspaceSizeCache(GetWorkspaceSizeQuery getWorkspaceSizeQuery)
{
    private readonly ConcurrentDictionary<int, long> _sizes = new();

    public long Get(int workspaceId)
    {
        return _sizes.GetOrAdd(
            workspaceId,
            getWorkspaceSizeQuery.Execute);
    }

    public long AddDelta(int workspaceId, long deltaInBytes)
    {
        Get(workspaceId);

        return _sizes.AddOrUpdate(
            workspaceId,
            deltaInBytes,
            (_, current) => current + deltaInBytes);
    }

    public void Set(int workspaceId, long sizeInBytes)
    {
        _sizes[workspaceId] = sizeInBytes;
    }

    public void Forget(int workspaceId)
    {
        _sizes.TryRemove(workspaceId, out _);
    }
}
