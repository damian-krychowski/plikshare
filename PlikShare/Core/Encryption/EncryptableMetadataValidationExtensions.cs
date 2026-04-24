using FluentValidation;

namespace PlikShare.Core.Encryption;

/// <summary>
/// FluentValidation rule forbidding user-supplied metadata names (folder/file/upload names)
/// from starting with <see cref="EncryptableMetadataExtensions.ReservedPrefix"/>. The prefix
/// is our exclusive namespace for encrypted metadata envelopes; allowing user input that
/// collides with it would muddy the "starts with prefix ⇒ encrypted" signal that tooling,
/// db dumps, and the decode routine rely on.
/// </summary>
public static class EncryptableMetadataValidationExtensions
{
    public static IRuleBuilderOptions<T, string> MustNotStartWithReservedMetadataPrefix<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Must(value => !value.StartsWith(EncryptableMetadataExtensions.ReservedPrefix, StringComparison.Ordinal))
            .WithMessage($"Name must not start with reserved prefix '{EncryptableMetadataExtensions.ReservedPrefix}'.");
    }
}
