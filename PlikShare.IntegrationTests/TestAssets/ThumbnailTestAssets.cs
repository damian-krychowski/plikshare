using System.Reflection;

namespace PlikShare.IntegrationTests.TestAssets;

/// <summary>
/// Embedded media bytes used by the thumbnail-generation integration tests. Lives in the test
/// project (NOT in PlikShare) — test fixtures have no business shipping inside the production
/// assembly.
/// </summary>
public static class ThumbnailTestAssets
{
    private static readonly Lazy<byte[]> RedVideoBytes = new(() =>
        LoadEmbedded("PlikShare.IntegrationTests.TestAssets.test_video_red_1s.mp4"));

    // Three real, multi-megapixel JPEG photos — big enough that the encoder must downscale to
    // each variant's target height (Mini=96, Small=400, Large=1600), so Mini < Small < Large in
    // the resulting WebP byte size. Bulk tests use them to exercise the queue with real bytes
    // rather than the synthetic Textract PNG.
    // Note: illustartion_1.jpg keeps the typo from the asset filename — the resource ID has to
    // match what's on disk byte-for-byte or GetManifestResourceStream returns null.
    private static readonly Lazy<byte[]> Illustration1Bytes = new(() =>
        LoadEmbedded("PlikShare.IntegrationTests.TestAssets.illustartion_1.jpg"));

    private static readonly Lazy<byte[]> Illustration2Bytes = new(() =>
        LoadEmbedded("PlikShare.IntegrationTests.TestAssets.illustration_2.jpg"));

    private static readonly Lazy<byte[]> Illustration3Bytes = new(() =>
        LoadEmbedded("PlikShare.IntegrationTests.TestAssets.illustration_3.jpg"));

    /// <summary>
    /// 1-second, 64×64, fast-start H.264 MP4 — a deterministic video the queue worker can demux
    /// without surprises. Faststart means moov-at-start so the demuxer never needs an end-of-file
    /// seek, exercising the happy path of <c>GenerateThumbnailsFromFile</c> after the executor
    /// stashes the source on disk.
    /// </summary>
    public static byte[] GetRedVideoBytes() => RedVideoBytes.Value;

    public static byte[] GetIllustration1Bytes() => Illustration1Bytes.Value;
    public static byte[] GetIllustration2Bytes() => Illustration2Bytes.Value;
    public static byte[] GetIllustration3Bytes() => Illustration3Bytes.Value;

    public static IReadOnlyList<(string FileName, byte[] Bytes)> AllIllustrations() =>
    [
        ("illustartion_1.jpg", Illustration1Bytes.Value),
        ("illustration_2.jpg", Illustration2Bytes.Value),
        ("illustration_3.jpg", Illustration3Bytes.Value),
    ];

    private static byte[] LoadEmbedded(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Thumbnail test asset embedded resource '{resourceName}' was not found.");

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);

        return memoryStream.ToArray();
    }
}
