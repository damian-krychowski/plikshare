namespace PlikShare.Uploads.FilePartUpload.Initiate.Contracts;

public record InitiateFilePartUploadResponseDto(
    string UploadPreSignedUrl,
    long StartsAtByte,
    long EndsAtByte,
    bool IsCompleteFilePartUploadCallbackRequired);