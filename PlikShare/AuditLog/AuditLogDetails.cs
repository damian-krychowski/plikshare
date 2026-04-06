namespace PlikShare.AuditLog;

public static class AuditLogDetails
{
    public static class Auth
    {
        public class SignedIn
        {
            public required string Method { get; init; }
        }

        public class Failed
        {
            public required string Reason { get; init; }
        }

        public class Sso
        {
            public required string ProviderName { get; init; }
        }
    }

    public static class User
    {
        public class Invited
        {
            public required List<string> Emails { get; init; }
        }

        public class Deleted
        {
            public required string TargetEmail { get; init; }
        }

        public class PermissionsAndRolesUpdated
        {
            public required string TargetEmail { get; init; }
            public required bool IsAdmin { get; init; }
            public required List<string> Permissions { get; init; }
        }

        public class LimitUpdated
        {
            public required long? Value { get; init; }
        }
    }

    public static class Settings
    {
        public class ValueChanged
        {
            public required string? Value { get; init; }
        }

        public class ToggleChanged
        {
            public required bool Value { get; init; }
        }

        public class DefaultPermissionsChanged
        {
            public required bool IsAdmin { get; init; }
            public required List<string> Permissions { get; init; }
        }

        public class SignUpCheckbox
        {
            public int? Id { get; init; }
            public required string Text { get; init; }
            public required bool IsRequired { get; init; }
        }
    }

    public static class EmailProvider
    {
        public class Created
        {
            public required string Name { get; init; }
            public required string Type { get; init; }
            public required string EmailFrom { get; init; }
        }

        public class Deleted
        {
            public required string ExternalId { get; init; }
        }

        public class NameUpdated
        {
            public required string ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class ActivationChanged
        {
            public required string ExternalId { get; init; }
        }

        public class ConfirmationEmailResent
        {
            public required string ExternalId { get; init; }
        }
    }

    public static class AuthProvider
    {
        public class Created
        {
            public required string Name { get; init; }
            public required string Type { get; init; }
        }

        public class Deleted
        {
            public required string ExternalId { get; init; }
        }

        public class NameUpdated
        {
            public required string ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class Updated
        {
            public required string ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class ActivationChanged
        {
            public required string ExternalId { get; init; }
        }

        public class PasswordLoginToggled
        {
            public required bool IsEnabled { get; init; }
        }
    }
}
