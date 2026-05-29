using System.Data.Common;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Metadata;

namespace PlikShare.Files.Thumbnails;

// Decides, inside a folder-listing row, whether a file has a Mini thumbnail child. The listing
// query pulls each file's thumbnail-child metadata blobs in a single correlated subquery
// (json_group_array(CAST(fi_metadata AS TEXT)) — the column is a BLOB holding UTF-8 of the
// encoded envelope). The Mini variant lives inside the encrypted envelope, so we decrypt
// app-side here — only for files that actually have child metadata (the subquery yields an empty
// array otherwise, so files with no thumbnails cost zero decryptions).
public static class MiniThumbnailMetadata
{
    public static bool HasMini(
        DbDataReader reader,
        int ordinal,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        var encodedChildren = reader.GetFromJsonOrNull<List<string>>(ordinal);

        if (encodedChildren is null || encodedChildren.Count == 0)
            return false;

        foreach (var encoded in encodedChildren)
        {
            if (string.IsNullOrEmpty(encoded))
                continue;

            var metadataJson = workspaceEncryptionSession.DecodeEncryptableMetadata(encoded);

            if (Json.Deserialize<FileMetadata>(metadataJson)
                is ThumbnailFileMetadata { Variant: ThumbnailVariant.Mini })
            {
                return true;
            }
        }

        return false;
    }
}
