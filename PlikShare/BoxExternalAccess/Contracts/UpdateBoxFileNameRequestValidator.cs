using FluentValidation;

namespace PlikShare.BoxExternalAccess.Contracts;

public class UpdateBoxFileNameRequestValidator : AbstractValidator<UpdateBoxFileNameRequestDto>
{
    public UpdateBoxFileNameRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}