using System.Data.Common;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Metadata;

namespace PlikShare.MediaProcessing;

public static class MiniThumbnailMetadata
{
    public static string? GetMiniEtag(
        DbDataReader reader,
        int ordinal,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        var encodedChildren = reader.GetFromJsonOrNull<List<string>>(ordinal);

        if (encodedChildren is null || encodedChildren.Count == 0)
            return null;

        foreach (var encoded in encodedChildren)
        {
            if (string.IsNullOrEmpty(encoded))
                continue;

            var metadataJson = workspaceEncryptionSession.DecodeMetadata(encoded);

            if (Json.Deserialize<FileMetadata>(metadataJson)
                is ThumbnailFileMetadata { Variant: ThumbnailVariant.Mini } mini)
            {
                return mini.Etag;
            }
        }

        return null;
    }
}
