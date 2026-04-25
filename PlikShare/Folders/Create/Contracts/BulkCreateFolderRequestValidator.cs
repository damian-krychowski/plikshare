using FluentValidation;
using PlikShare.Core.Encryption;

namespace PlikShare.Folders.Create.Contracts;

public class BulkCreateFolderRequestValidator : AbstractValidator<BulkCreateFolderRequestDto>
{
    public BulkCreateFolderRequestValidator()
    {
        RuleForEach(x => x.FolderTrees)
            .SetValidator(new FolderTreeDtoValidator());
    }
}

public class FolderTreeDtoValidator : AbstractValidator<FolderTreeDto>
{
    public FolderTreeDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MustNotStartWithReservedMetadataPrefix();

        RuleForEach(x => x.Subfolders)
            .SetValidator(this);
    }
}
