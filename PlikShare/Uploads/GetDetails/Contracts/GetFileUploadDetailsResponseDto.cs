using PlikShare.Uploads.Algorithm;

namespace PlikShare.Uploads.GetDetails.Contracts;

public class GetFileUploadDetailsResponseDto
{
    public required List<int> AlreadyUploadedPartNumbers { get; init; }
    public required UploadAlgorithm Algorithm { get; init; }
    public required int ExpectedPartsCount { get; init; }
};