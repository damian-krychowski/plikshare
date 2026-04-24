using FluentValidation;
using PlikShare.Core.Encryption;

namespace PlikShare.Files.Rename.Contracts;

public class UpdateFileNameRequestValidator : AbstractValidator<UpdateFileNameRequestDto>
{
    public UpdateFileNameRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MustNotStartWithReservedMetadataPrefix();
    }
}