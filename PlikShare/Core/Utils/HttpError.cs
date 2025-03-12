using Microsoft.AspNetCore.Http.HttpResults;
using PlikShare.Boxes.Id;
using PlikShare.Core.Encryption;
using PlikShare.EmailProviders.Id;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.GeneralSettings;
using PlikShare.Integrations.Id;
using PlikShare.Storages.Id;
using PlikShare.Uploads.Id;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Id;

namespace PlikShare.Core.Utils;

public class HttpError
{
    public required string Message { get; set; }
    public required string Code { get; set; }
}

public class HttpErrorWithDetails : HttpError
{
    public required string InnerError { get; set; }
}

public static class HttpErrors
{
    public static class Workspace
    {
        public static BadRequest<HttpError> BrokenExternalId(string externalId) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "broken-workspace-external-id",
                Message = $"WorkspaceExternalId is in wrong format: '{externalId}'."
            });

        public static BadRequest<HttpError> MissingExternalId() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "missing-workspace-external-id",
                Message = "WorkspaceExternalId is missing"
            });

        public static NotFound<HttpError> NotFound(WorkspaceExtId externalId) =>
            TypedResults.NotFound(new HttpError
            {
                Code = "workspace-doesnt-exist",
                Message = $"Workspace with externalId '{externalId}' was not found."
            });

        public static NotFound<HttpError> NotFound() =>
            TypedResults.NotFound(new HttpError
            {
                Code = "workspace-doesnt-exist",
                Message = $"Workspace was not found."
            });

        public static NotFound<HttpError> MemberNotFound(UserExtId userId, WorkspaceExtId workspaceExternalId) =>
            TypedResults.NotFound(new HttpError
            {
                Code = "workspace-member-doesnt-exist",
                Message = $"Member with externalId '{userId}' of Workspace '{workspaceExternalId}' was not found."
            });


        public static NotFound<HttpError> InvitationNotFound(WorkspaceExtId externalId) =>
            TypedResults.NotFound(new HttpError
            {
                Code = "workspace-invitation-doesnt-exist",
                Message = $"Invitation for Workspace with externalId '{externalId}' was not found."
            });

        public static BadRequest<HttpError> BucketNotReady(WorkspaceExtId externalId) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "workspace-bucket-is-not-ready-yet",
                Message = $"Cannot initiate file uploads because Workspace '{externalId}' bucket was not yet created."
            });

        public static BadRequest<HttpError> BucketNotReady() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "workspace-bucket-is-not-ready-yet",
                Message = "Cannot initiate file uploads because Workspace bucket was not yet created."
            });

        public static BadRequest<HttpError> InvitationAlreadyAccepted(WorkspaceExtId externalId) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "workspace-invitation-was-already-accepted",
                Message = $"Invitation for Workspace with externalId '{externalId}' was already accepted."
            });

        public static BadRequest<HttpError> CannotDelete(WorkspaceExtId externalId) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "cannot-delete-workspace",
                Message = $"Workspace with externalId '{externalId}' does not belong to the user."
            });

        public static BadRequest<HttpError> MemberNotInvited(WorkspaceExtId externalId) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "cannot-leave-workspace",
                Message =
                    $"User was not invited to the Workspace with externalId '{externalId}' so they cannot leave it."
            });

        public static BadRequest<HttpError> UsedByIntegration(WorkspaceExtId externalId) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "workspace-used-by-integration",
                Message = $"Workspace with externalId '{externalId}' is used by integration and cannot be deleted."
            });
    }

    public static class Storage
    {
        public static NotFound<HttpError> NotFound(StorageExtId externalId) =>
            TypedResults.NotFound(new HttpError
            {
                Code = "storage-doesnt-exist",
                Message = $"Storage with externalId '{externalId}' was not found."
            });

        public static BadRequest<HttpError> WorkspaceOrIntegrationAttached(StorageExtId externalId) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "storage-has-workspaces-or-integrations-attached",
                Message = $"There are some Workspaces/Integrations using Storage with externalId '{externalId}' " +
                          $"and thus it cannot be deleted"
            });

        public static BadRequest<HttpError> NameNotUnique(string name) => TypedResults.BadRequest(new HttpError
        {
            Code = "storage-name-is-not-unique",
            Message = $"Name '{name}' is not unique"
        });

        public static BadRequest<HttpError> ConnectionFailed() => TypedResults.BadRequest(new HttpError
        {
            Code = "storage-connection-failed",
            Message = "Could not connect to the storage with given credentials"
        });

        public static BadRequest<HttpError> InvalidUrl(string url) => TypedResults.BadRequest(new HttpError
        {
            Code = "storage-url-invalid",
            Message = $"URL '{url}' is not valid"
        });

        public static BadRequest<HttpError> VolumeNotFound(string volumePath) => TypedResults.BadRequest(new HttpError
        {
            Code = "volume-not-found",
            Message = $"Volume '{volumePath}' was not found"
        });
    }

    public static class Folder
    {
        public static NotFound<HttpError> NotFound(IEnumerable<FolderExtId>? missingFolders) =>
            NotFound(missingFolders?.Select(x => x.Value));

        public static NotFound<HttpError> NotFound(IEnumerable<string>? missingFolders) =>
            TypedResults.NotFound(new HttpError
            {
                Code = "folder-doesnt-exist",
                Message = missingFolders is null || !missingFolders.Any()
                    ? "Some folders were not found."
                    : $"Folders with ExternalIds: '{string.Join(", ", missingFolders)}' were not found."
            });
        
        public static NotFound<HttpError> NotFound(FolderExtId externalId) => TypedResults.NotFound(new HttpError
        {
            Code = "folder-doesnt-exist",
            Message = $"Folder with externalId '{externalId}' was not found."
        });

        public static NotFound<HttpError> NotFound(FolderExtId? externalId)
        {
            if (externalId is not null)
                return NotFound(externalId.Value);

            return TypedResults.NotFound(new HttpError
            {
                Code = "folder-doesnt-exist",
                Message = $"Folder was not found."
            });
        }

        public static BadRequest<HttpError> DuplicatedNamesOnInput(IEnumerable<int> temporaryIdsWithDuplications) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "duplicated-names-on-input",
                Message = $"Items with following temporary ids have duplicated names: {string.Join(",", temporaryIdsWithDuplications)}"
            });

        public static BadRequest<HttpError> DuplicatedTemporaryIds(IEnumerable<int> temporaryIdsWithDuplications) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "duplicated-temporary-ids",
                Message = $"Following temporary ids are duplicated: {string.Join(",", temporaryIdsWithDuplications)}"
            });

        public static NotFound<HttpError> SomeFolderNotFound() =>
            TypedResults.NotFound(new HttpError
            {
                Code = "folder-doesnt-exist",
                Message = "Some folder was not found"
            });

        public static NotFound<HttpError> SomeFileNotFound() =>
            TypedResults.NotFound(new HttpError
            {
                Code = "file-doesnt-exist",
                Message = "Some file was not found"
            });

        public static NotFound<HttpError> SomeFileUploadNotFound() =>
            TypedResults.NotFound(new HttpError
            {
                Code = "file-upload-doesnt-exist",
                Message = "Some file upload was not found"
            });

        public static BadRequest<HttpError> CannotMoveFoldersToOwnSubfolders() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "cannot-move-folders-to-own-subfolders",
                Message = "Some folders were attempted to be moved to its own subfolders."
            });
    }

    public static class Upload
    {
        public static NotFound<HttpError> NotFound(FileUploadExtId uploadId) =>
            TypedResults.NotFound(new HttpError
            {
                Code = "file-upload-doesnt-exist",
                Message = $"File upload with externalId '{uploadId}' was not found."
            });

        public static NotFound<HttpError> PartNotAllowed(FileUploadExtId externalId, int partNumber) =>
            TypedResults.NotFound(new HttpError
            {
                Code = "file-upload-part-number-not-allowed",
                Message = $"File upload part with number: '{partNumber}' for upload '{externalId}' is not allowed."
            });

        public static BadRequest<HttpError> NotCompleted(FileUploadExtId externalId) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "file-upload-not-yet-completed",
                Message = $"File upload with externalId: '{externalId}' was not yet fully completed."
            });

        public static BadRequest<HttpError> InvalidContentLength(long? actual, long? expected) => TypedResults.BadRequest(new HttpError
        {
            Code = "invalid-content-length",
            Message = $"Content length {actual} does not match the expected file size {expected}."
        });
    }

    public static class User
    {
        public static NotFound<HttpError> NotFound(UserExtId externalId) =>
            TypedResults.NotFound(new HttpError
            {
                Code = "user-doesnt-exist",
                Message = $"User with externalId '{externalId}' was not found"
            });

        public static BadRequest<HttpError> CannotModifyOwnUser(UserExtId externalId) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "cannot-modify-own-user",
                Message = $"User with externalId '{externalId}' cannot modify his own permissions/roles"
            });

        public static BadRequest<HttpError> CannotModifyAdminUser(UserExtId externalId) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "cannot-modify-admin-user",
                Message = $"User with externalId '{externalId}' is admin and can be modified only by App Owner"
            });

        public static BadRequest<HttpError> CannotDeleteUserWithDependencies(UserExtId externalId) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "user-has-outstanding-dependencies",
                Message =
                    $"User with externalId '{externalId}' has some outstanding dependencies (like workspaces) and cannot be deleted."
            });
    }

    public static class Integration
    {
        public static BadRequest<HttpError> NameNotUnique(string name) => TypedResults.BadRequest(new HttpError
        {
            Code = "integration-name-not-unique",
            Message = $"Name '{name}' is not unique"
        });

        public static NotFound<HttpError> NotFound(IntegrationExtId externalId) => TypedResults.NotFound(new HttpError
        {
            Code = "integration-doesnt-exist",
            Message = $"Integration with externalId '{externalId}' was not found"
        });
    }

    public static class File
    {
        public static NotFound<HttpError> NotFound(FileExtId fileExternalId) =>
            TypedResults.NotFound(new HttpError
            {
                Code = "file-doesnt-exist",
                Message = $"File with externalId '{fileExternalId}' was not found."
            });

        public static BadRequest<HttpError> WrongFileExtension(FileExtId fileExternalId, string expectedExtension) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "wrong-file-extension",
                Message = $"File externalId '{fileExternalId}' has wrong extensions. " +
                          $"Expected extension is {expectedExtension}"
            });

        public static BadRequest<HttpError> ZipFileBroken(FileExtId fileExternalId) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "zip-file-is-broken",
                Message = $"File externalId '{fileExternalId}' is a zip file but it is internally broken."
            });

        public static NotFound<HttpError> CommentNotFound(FileArtifactExtId commentExternalId) =>
            TypedResults.NotFound(new HttpError
            {
                Code = "file-comment-doesnt-exist",
                Message = $"File Comment with externalId '{commentExternalId}' was not found."
            });

        public static NotFound<HttpError> SomeFilesNotFound(IEnumerable<FileExtId> notFoundFileExternalIds) =>
            TypedResults.NotFound(new HttpError
            {
                Code = "some-files-dont-exist",
                Message = $"Files with externalIds {string.Join(", ", notFoundFileExternalIds)} were not found."
            });

        public static BadRequest<HttpError> InvalidContentDisposition(string contentDisposition) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "invalid-content-disposition",
                Message = $"Content disposition '{contentDisposition}' is invalid."
            });

        public static BadRequest<HttpError> ExpectedMultipartFormDataContent() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "invalid-content-type",
                Message = "Request content type must be multipart/form-data."
            });

        public static BadRequest<HttpError> MissingFile() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "missing-file",
                Message = "No file was uploaded."
            });

        public static BadRequest<HttpError> MissingFileName() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "missing-file-name",
                Message = "File name is required."
            });

        public static BadRequest<HttpError> PayloadTooBig(long payloadSize) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "payload-too-big",
                Message =
                    $"File upload payload cannot be greater than {Aes256GcmStreaming.MaximumPayloadSize} bytes, but found {payloadSize} bytes"
            });

        public static BadRequest<HttpError> MissingAttachmentFileExternalId() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "missing-attachment-file-external-id",
                Message = "Attachment fileExternalId is required."
            });

        public static BadRequest<HttpError> MissingContentTypeBoundary() => TypedResults.BadRequest(new HttpError
            {
                Code = "invalid-boundary",
                Message = "Missing content-type boundary"
            });

        public static BadRequest<HttpError> MissingRequestHeader(string headerName) => TypedResults.BadRequest(
            new HttpError
            {
                Code = "missing-request-header",
                Message = $"Invalid or missing {headerName} header"
            });


        public static BadRequest<HttpError> NoFilesToUpload() => TypedResults.BadRequest(
            new HttpError
            {
                Code = "no-files-to-upload",
                Message = $"No valid files were provided in the multi-file-direct-upload request"
            });
    }

    public static class EmailProvider
    {
        public static NotFound<HttpError> NotFound(EmailProviderExtId externalId) =>
            TypedResults.NotFound(new HttpError
            {
                Code = "email-provider-doesnt-exist",
                Message = $"Email Provider with externalId '{externalId}' was not found"
            });

        public static BadRequest<HttpError> NameNotUnique(string name) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "email-provider-name-is-not-unique",
                Message = $"Name '{name}' is not unique"
            });

        public static BadRequest<HttpError> NotConfirmed() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "email-provider-is-not-confirmed",
                Message = "Cannot activate Email Provider which was not yet confirmed."
            });

        public static NotFound<HttpError> AlreadyConfirmed(EmailProviderExtId externalId) =>
            TypedResults.NotFound(new HttpError
            {
                Code = "email-provider-is-already-confirmed",
                Message = $"Email Provider with externalId '{externalId}' was already confirmed"
            });

        public static BadRequest<HttpError> CouldNotSendTestEmail() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "email-provider-failure",
                Message = "Could not send test with given provider details"
            });

        public static BadRequest<HttpError> WrongConfirmationCode(string confirmationCode) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "email-provider-wrong-confirmation-code",
                Message = $"Confirmation code '{confirmationCode}' is wrong"
            });

        public static BadRequest<HttpErrorWithDetails> CouldNotSendTestEmailWithDetails(string innerError, string providerType) =>
            TypedResults.BadRequest(new HttpErrorWithDetails
            {
                Code = "email-provider-failure",
                Message = $"Could not send test email with {providerType} with given credentials",
                InnerError = innerError
            });
    }

    public static class BulkDownload
    {
        public static BadRequest<HttpError> InvalidPayload() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "invalid-bulk-download-payload",
                Message = "System could not verify the payload provided in the url"
            });

        public static NotFound<HttpError> WorkspaceNotFound() =>
            TypedResults.NotFound(new HttpError
            {
                Code = "workspace-doesnt-exist",
                Message = "Workspace was not found"
            });
    }
    
    public static class BoxLink
    {
        public static NotFound<HttpError> NotFound(BoxLinks.Id.BoxLinkExtId externalId) =>
            TypedResults.NotFound(new HttpError
            {
                Code = "box-link-doesnt-exist",
                Message = $"Box-link with externalId '{externalId}' was not found."
            });

        public static BadRequest<HttpError> InvalidExternalId(string actualValue) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "invalid-box-link-external-id",
                Message = $"BoxLinkExternalId is invalid: '{actualValue}'"
            });

        public static BadRequest<HttpError> MissingExternalId() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "missing-box-link-external-id",
                Message = $"BoxLinkExternalId is missing."
            });
    }

    public static class Box
    {
        public static NotFound<HttpError> NotFound(BoxExtId externalId) =>
            TypedResults.NotFound(new HttpError
            {
                Code = "box-doesnt-exist",
                Message = $"Box with externalId '{externalId}' was not found."
            });

        public static NotFound<HttpError> MemberNotFound(BoxExtId boxExternalId, UserExtId memberExternalId) =>
            TypedResults.NotFound(new HttpError
            {
                Code = "box-member-doesnt-exist",
                Message = $"Member with externalId '{memberExternalId}' was not found for Box {boxExternalId}."
            });

        public static NotFound<HttpError> InvitationNotFound(BoxExtId boxExternalId) =>
            TypedResults.NotFound(new HttpError
            {
                Code = "box-invitation-doesnt-exist",
                Message = $"Invitation for Box with externalId '{boxExternalId}' was not found."
            });

        public static BadRequest<HttpError> InvalidExternalId(string actualValue) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "invalid-box-external-id",
                Message = $"BoxExternalId is invalid: '{actualValue}'"
            });

        public static BadRequest<HttpError> MissingExternalId() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "missing-box-external-id",
                Message = $"BoxExternalId is missing."
            });

        public static BadRequest<HttpError> InvalidAccessCode() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "invalid-access-code",
                Message = $"Provided access-code is invalid."
            });
    }
    
    public static class ArtificialIntelligence
    {
        public static NotFound<HttpError> ConversationNotFound(FileArtifactExtId fileArtifactExternalId) =>
            TypedResults.NotFound(new HttpError
            {
                Code = "ai-conversation-doesnt-exist",
                Message = $"AiConversation associated with FileArtifact '{fileArtifactExternalId}' was not found."
            });

        public static BadRequest<HttpError> StaleConversationCounter() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "stale-conversation-counter",
                Message = "Some other messages were created in the meantime."
            });
    }

    public static class ProtectedPayload
    {
        public static BadRequest<HttpError> Missing() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "missing-protected-payload",
                Message = "Protected payload is required"
            });

        public static BadRequest<HttpError> Invalid() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "invalid-protected-payload",
                Message = "System could not verify the payload provided in the url"
            });

        public static BadRequest<HttpError> MissingContentLengthHeader() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "missing-content-length-header",
                Message = "Content-Length header is required"
            });

        public static BadRequest<HttpError> MissingContentTypeHeader() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "missing-content-type-header",
                Message = "Content-Type header is required"
            });
    }

    public static class LegalFiles
    {
        public static NotFound<HttpError> TermsOfServiceNotFound() =>
            TypedResults.NotFound(new HttpError
            {
                Code = "terms-of-service-not-found",
                Message = "Terms of service file was not found"
            });

        public static NotFound<HttpError> PrivacyPolicyNotFound() =>
            TypedResults.NotFound(new HttpError
            {
                Code = "privacy-policy-not-found",
                Message = "Privacy policy file was not found"
            });

        public static NotFound<HttpError> NotFound(string code, string message) =>
            TypedResults.NotFound(new HttpError
            {
                Code = code,
                Message = message
            });
    }

    public static class AwsTextract
    {
        public static BadRequest<HttpError> AnalysisTimeout() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "analysis-timeout",
                Message = "The document analysis operation timed out."
            });

        public static BadRequest<HttpError> AccessDenied(string errorMessage) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "aws-textract-access-denied",
                Message = errorMessage
            });

        public static BadRequest<HttpError> S3AccessDenied(string errorMessage) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "aws-s3-access-denied",
                Message = errorMessage
            });

        public static BadRequest<HttpError> InvalidSecretAccessKey(string errorMessage) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "aws-textract-invalid-secret-access-key",
                Message = errorMessage
            });

        public static BadRequest<HttpError> UnrecognizedAccessKey(string errorMessage) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "aws-textract-unrecognized-access-key",
                Message = errorMessage
            });
    }

    public static class OpenAiChatGpt
    {
        public static BadRequest<HttpError> InvalidApiKey() => TypedResults.BadRequest(new HttpError
            {
                Code = "openai-chatgpt-invalid-api-key",
                Message = $"Provided ApiKey is invalid"
            });
    }

    public static class GeneralSettings
    {
        public static BadRequest<HttpError> WrongApplicationSignUpValue(string value) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "application-sign-up-wrong-value",
                Message = $"Setting '{AppSettings.SignUpSetting.Key}' value can be either '{AppSettings.SignUpSetting.Everyone}' or '{AppSettings.SignUpSetting.OnlyInvitedUsers}' " +
                          $"but found '{value}'"
            });

        public static BadRequest<HttpError> FileIsNullOrEmpty() =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "file-is-null-or-empty",
                Message = "File to upload cannot be null or empty"
            });

        public static BadRequest<HttpError> WrongFileType(string documentType) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "wrong-file-type",
                Message = $"Only PDF can be uploaded for {documentType}"
            });

        public static BadRequest<HttpError> DuplicatedFileName(string currentDocument, string otherDocument) =>
            TypedResults.BadRequest(new HttpError
            {
                Code = "duplicated-file-name",
                Message = $"{currentDocument} must have different name than {otherDocument}"
            });
    }
}