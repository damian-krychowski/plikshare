using System.Data.Common;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Metadata;

namespace PlikShare.MediaProcessing;

public static class ImageDimensionsMetadata
{
    public readonly record struct Dimensions(int Width, int Height);

    public static Dimensions? Read(
        DbDataReader reader,
        int ordinal,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        var metadataJson = reader.DecodeEncryptableBlobOrNull(
            ordinal,
            workspaceEncryptionSession);

        if (metadataJson is null)
            return null;

        if (Json.Deserialize<FileMetadata>(metadataJson) is ImageDimensionsFileMetadata d)
            return new Dimensions(d.Width, d.Height);

        return null;
    }
}
