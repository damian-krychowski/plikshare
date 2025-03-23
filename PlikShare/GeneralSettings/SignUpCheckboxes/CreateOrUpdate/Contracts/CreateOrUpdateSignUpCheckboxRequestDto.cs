namespace PlikShare.GeneralSettings.SignUpCheckboxes.CreateOrUpdate.Contracts;

public class CreateOrUpdateSignUpCheckboxRequestDto
{
    public required int? Id { get; init; }
    public required string Text { get; init; }
    public required bool IsRequired { get; init; }
}

public class CreateOrUpdateSignUpCheckboxResponseDto
{
    public required int NewId { get; init; }
}