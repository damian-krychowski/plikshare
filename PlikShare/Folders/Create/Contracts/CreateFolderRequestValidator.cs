using FluentValidation;

namespace PlikShare.Folders.Create.Contracts;

public class CreateFolderRequestValidator : AbstractValidator<CreateFolderRequestDto>
{
    public CreateFolderRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty();
    }
}