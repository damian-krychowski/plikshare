using FluentValidation;
using PlikShare.Core.Encryption;

namespace PlikShare.Uploads.Initiate.Contracts;

public class BulkInitiateFileUploadRequestValidator : AbstractValidator<BulkInitiateFileUploadRequestDto>
{
    public BulkInitiateFileUploadRequestValidator()
    {
        RuleForEach(x => x.Items)
            .SetValidator(new BulkInitiateFileUploadItemValidator());
    }
}

public class BulkInitiateFileUploadItemValidator : AbstractValidator<BulkInitiateFileUploadItemDto>
{
    public BulkInitiateFileUploadItemValidator()
    {
        RuleFor(x => x.FileNameWithExtension)
            .NotEmpty()
            .MustNotStartWithReservedMetadataPrefix();
    }
}
