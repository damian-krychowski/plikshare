using PlikShare.Files.Id;

namespace PlikShare.Files.Preview.Comment.CreateComment.Contracts;

public record CreateFileCommentRequestDto(
    FileArtifactExtId ExternalId,
    string ContentJson);
