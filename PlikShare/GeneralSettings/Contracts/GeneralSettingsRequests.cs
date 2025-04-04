namespace PlikShare.GeneralSettings.Contracts;

public class SetSettingRequest
{
    public string? Value { get; set; }
}

public class SetNewUserDefaultMaxWorkspaceNumberRequestDto
{
    public required int? Value { get; init; }
}

public class SetNewUserDefaultMaxWorkspaceSizeInBytesRequestDto
{
    public required long? Value { get; init; }
}

public class SetAlertSettingReuqest
{
    public required bool IsTurnedOn { get; init; }
}