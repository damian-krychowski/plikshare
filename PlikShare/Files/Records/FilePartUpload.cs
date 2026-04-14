using PlikShare.Uploads.Algorithm;

namespace PlikShare.Files.Records;

public readonly record struct FilePartUpload(
    FilePart Part,
    UploadAlgorithm UploadAlgorithm)
{
    public static FilePartUpload First(int sizeInBytes, UploadAlgorithm algorithm)
    {
        return new FilePartUpload(
            Part: new FilePart(
                Number: 1, 
                SizeInBytes: sizeInBytes), 
            UploadAlgorithm: algorithm);
    }
}

public readonly record struct FilePart(int Number, int SizeInBytes)
{
    public int Number { get; init; } = Number >= 1
        ? Number
        : throw new ArgumentOutOfRangeException(nameof(Number), Number,
            "Part number must be 1-based (S3 convention).");
}
