using PlikShare.ArtificialIntelligence.Id;
using PlikShare.Files.Id;
using PlikShare.Integrations.Aws.Textract;
using PlikShare.Integrations.Aws.Textract.Id;
using PlikShare.Integrations.Aws.Textract.Jobs;
using PlikShare.Integrations.Id;

namespace PlikShare.Files.Preview.GetDetails.Contracts;

public enum FilePreviewDetailsField
{
    Note,
    Comments,
    TextractResultFiles,
    PendingTextractJobs,
    AiConversations,
    Attachments
}

public class GetFilePreviewDetailsResponseDto
{
    public required FilePreviewNoteDto? Note { get; init; }
    public required List<FilePreviewCommentDto>? Comments { get; init; }
    public required List<FilePreviewTextractResultFile>? TextractResultFiles { get; init; }
    public required List<FilePreviewPendingTextractJob>? PendingTextractJobs { get; init; }
    public required List<FilePreviewAiConversation>? AiConversations { get; init; }
    public required List<FilePreviewAttachmentFile>? Attachments { get; init; }
}

public class FilePreviewNoteDto
{
    public required string ContentJson {get;init;}
    public required DateTimeOffset ChangedAt {get;init;}
    public required string ChangedBy { get; init; }
}

public class FilePreviewCommentDto
{
    public required FileArtifactExtId ExternalId { get; init; }
    public required string ContentJson { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string CreatedBy { get; init; }
    public required bool WasEdited { get; init; }
}

public class FilePreviewAttachmentFile
{
    public required FileExtId ExternalId { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public required long SizeInBytes { get; init; }
    public required bool WasUploadedByUser { get; init; }
}

public class FilePreviewTextractResultFile
{
    public required FileExtId ExternalId { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public required long SizeInBytes { get; init; }
    public required TextractFeature[] Features { get; init; }
    public required bool WasUploadedByUser { get; init; }
}

public class FilePreviewPendingTextractJob
{
    public required TextractJobExtId ExternalId { get; init; }
    public required TextractJobStatus Status { get; init; }
    public required TextractFeature[] Features { get; init; }
}

public class FilePreviewAiConversation
{
    public required FileArtifactExtId FileArtifactExternalId { get; init; }
    public required AiConversationExtId AiConversationExternalId { get; init; }
    public required IntegrationExtId AiIntegrationExternalId { get; init; }
    public required bool IsWaitingForAiResponse { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string CreatedBy { get; init; }
    public required int ConversationCounter { get; init; }
    public required string? Name { get; init; }
}