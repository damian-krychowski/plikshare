using PlikShare.Uploads.FilePartUpload.Initiate.Contracts;

namespace PlikShare.BoxExternalAccess.Contracts;

public record InitiateBoxFilePartUploadResponseDto(
    string UploadPreSignedUrl,
    long StartsAtByte,
    long EndsAtByte,
    CompleteFilePartUploadCallbackDto? CompleteCallback);
