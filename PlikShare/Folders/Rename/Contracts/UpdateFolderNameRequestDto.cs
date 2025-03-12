using FluentValidation;

namespace PlikShare.Folders.Rename.Contracts;

public record UpdateFolderNameRequestDto(
    string Name);

public class UpdateFolderNameRequestValidator : AbstractValidator<UpdateFolderNameRequestDto>
{
    public UpdateFolderNameRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}