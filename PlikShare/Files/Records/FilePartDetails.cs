using PlikShare.Uploads.Algorithm;

namespace PlikShare.Files.Records;

public readonly record struct FilePartDetails(
    int Number,
    int SizeInBytes,
    UploadAlgorithm UploadAlgorithm)
{
    public static FilePartDetails First(
        int sizeInBytes,
        UploadAlgorithm uploadAlgorithm)
    {
        return new FilePartDetails(1, sizeInBytes, uploadAlgorithm);
    }
}