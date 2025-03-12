namespace PlikShare.BoxExternalAccess.Contracts;

public record InitiateBoxFilePartUploadResponseDto(
    string UploadPreSignedUrl,
    long StartsAtByte,
    long EndsAtByte,
    bool IsCompleteFilePartUploadCallbackRequired);