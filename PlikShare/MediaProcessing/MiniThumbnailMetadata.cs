using System.Data.Common;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Files.Metadata;

namespace PlikShare.MediaProcessing;

public static class MiniThumbnailMetadata
{
    public static string? GetMiniEtag(
        DbDataReader reader,
        int ordinal,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        return GetMiniEtag(
            encodedChildren: reader.GetFromJsonOrNull<List<string>>(ordinal),
            workspaceEncryptionSession: workspaceEncryptionSession);
    }

    public static string? GetMiniEtag(
        List<string>? encodedChildren,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        if (encodedChildren is null || encodedChildren.Count == 0)
            return null;

        foreach (var encoded in encodedChildren)
        {
            if (string.IsNullOrEmpty(encoded))
                continue;

            var metadataJson = workspaceEncryptionSession.DecodeMetadata(encoded);

            var etag = FileMetadataJsonScanner.GetThumbnailMiniEtag(metadataJson);

            if (etag is not null)
                return etag;
        }

        return null;
    }
}
