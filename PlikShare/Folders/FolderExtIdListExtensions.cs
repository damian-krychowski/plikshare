using PlikShare.Folders.Id;

namespace PlikShare.Folders;

public static class FolderExtIdListExtensions
{
    public static int ComputeSequenceHash(this IList<FolderExtId> ids)
    {
        var hash = new HashCode();

        for (var i = 0; i < ids.Count; i++)
            hash.Add(ids[i]);
            
        return hash.ToHashCode();
    }
}
