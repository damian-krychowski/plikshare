namespace PlikShare.MediaProcessing.Generation;

public enum DownloadImageFormat
{
    Jpeg = 0,
    Png = 1,
    Webp = 2
}

public static class DownloadImageFormatExtensions
{
    extension(DownloadImageFormat format)
    {
        public string FileExtension => format switch
        {
            DownloadImageFormat.Jpeg => ".jpg",
            DownloadImageFormat.Png => ".png",
            DownloadImageFormat.Webp => ".webp",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

        public string ContentType => format switch
        {
            DownloadImageFormat.Jpeg => "image/jpeg",
            DownloadImageFormat.Png => "image/png",
            DownloadImageFormat.Webp => "image/webp",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }
}
