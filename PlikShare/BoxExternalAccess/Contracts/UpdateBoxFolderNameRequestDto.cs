using FluentValidation;

namespace PlikShare.BoxExternalAccess.Contracts;

public record UpdateBoxFolderNameRequestDto(
    string Name);

public class UpdateAccessCodeFolderNameRequestValidator : AbstractValidator<UpdateBoxFolderNameRequestDto>
{
    public UpdateAccessCodeFolderNameRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}