using PlikShare.Core.Encryption;
using PlikShare.Files.Metadata;
using PlikShare.Files.Metadata.Contracts;

namespace PlikShare.MediaProcessing;

public static class ThumbnailEtagsMetadata
{
    public readonly record struct Etags(string? Mini, string? Small, string? Large)
    {
        public bool IsEmpty => Mini is null && Small is null && Large is null;
    }

    public static Etags GetEtags(
        List<string>? encodedChildren,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        if (encodedChildren is null || encodedChildren.Count == 0)
            return default;

        string? mini = null;
        string? small = null;
        string? large = null;

        foreach (var encoded in encodedChildren)
        {
            if (string.IsNullOrEmpty(encoded))
                continue;

            var metadataJson = workspaceEncryptionSession.DecodeMetadata(encoded);

            var variantEtag = FileMetadataJsonScanner.GetThumbnailVariantEtag(
                metadataJson);

            if (variantEtag is not { } ve)
                continue;

            switch (ve.Variant)
            {
                case ThumbnailVariant.Mini:
                    mini ??= ve.Etag;
                    break;

                case ThumbnailVariant.Small:
                    small ??= ve.Etag;
                    break;

                case ThumbnailVariant.Large:
                    large ??= ve.Etag;
                    break;
            }
        }

        return new Etags(mini, small, large);
    }

    public static ThumbnailMetadataDto? PrepareDto(
        List<string>? encodedChildren,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        var etags = GetEtags(
            encodedChildren,
            workspaceEncryptionSession);

        if (etags.IsEmpty)
            return null;

        return new ThumbnailMetadataDto
        {
            MiniEtag = etags.Mini,
            SmallEtag = etags.Small,
            LargeEtag = etags.Large
        };
    }
}
