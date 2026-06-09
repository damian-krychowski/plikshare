using System.Data.Common;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
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

        var dimensions = FileMetadataJsonScanner.GetImageDimensions(
            metadataJson);

        return dimensions is { } d
            ? new Dimensions(d.Width, d.Height)
            : null;
    }
}
