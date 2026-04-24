using FluentValidation;
using PlikShare.Core.Encryption;

namespace PlikShare.BoxExternalAccess.Contracts;

public class UpdateBoxFileNameRequestValidator : AbstractValidator<UpdateBoxFileNameRequestDto>
{
    public UpdateBoxFileNameRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MustNotStartWithReservedMetadataPrefix();
    }
}