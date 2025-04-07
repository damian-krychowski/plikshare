using Microsoft.Data.Sqlite;
using PlikShare.Core.Authorization;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.IdentityProvider;
using PlikShare.Core.SQLite;

namespace PlikShare.GeneralSettings;

public class AppSettings(PlikShareDb plikShareDb)
{
    public class SignUpCheckbox
    {
        public required int Id { get; init; }
        public required string Text { get; init; }
        public required bool IsRequired { get; init; }
    }

    public record AlertOnNewUserRegisteredSetting(bool IsTurnedOn)
    {
        public const string Key = "alert-on-new-user-registered";
        public static AlertOnNewUserRegisteredSetting Default => new(false);

        public static AlertOnNewUserRegisteredSetting FromString(string value)
        {
            if (bool.TryParse(value, out var boolValue))
            {
                return new(IsTurnedOn: boolValue);
            }

            throw new ArgumentOutOfRangeException(
                $"Setting '{Key}' value was expected to be an boolean but found '{value}'");
        }

        public string Serialize()
        {
            return IsTurnedOn.ToString();
        }
    }

    public record NewUserDefaultMaxWorkspaceNumberSetting(int? Value)
    {
        public const string Key = "new-user-default-max-workspace-number";
        public static NewUserDefaultMaxWorkspaceNumberSetting Default => new(0);

        public static NewUserDefaultMaxWorkspaceNumberSetting FromString(string value)
        {
            if(int.TryParse(value, out var intValue))
            {
                if (intValue == -1)
                    return new(Value: null);

                return new(Value: intValue);
            }

            throw new ArgumentOutOfRangeException(
                $"Setting '{Key}' value was expected to be an int32 but found '{value}'");
        }

        public string Serialize()
        {
            return Value.HasValue
                ? Value.Value.ToString()
                : "-1";
        }
    }

    public record NewUserDefaultMaxWorkspaceSizeInBytesSetting(long? Value)
    {
        public const string Key = "new-user-default-max-workspace-size-in-bytes";
        public static NewUserDefaultMaxWorkspaceSizeInBytesSetting Default => new(0);

        public static NewUserDefaultMaxWorkspaceSizeInBytesSetting FromString(string value)
        {
            if (long.TryParse(value, out var longValue))
            {
                if (longValue == -1)
                    return new(Value: null);

                return new(Value: longValue);
            }

            throw new ArgumentOutOfRangeException(
                $"Setting '{Key}' value was expected to be an int64 but found '{value}'");
        }

        public string Serialize()
        {
            return Value.HasValue
                ? Value.Value.ToString()
                : "-1";
        }
    }

    public record NewUserDefaultMaxWorkspaceTeamMembersSetting(int? Value)
    {
        public const string Key = "new-user-default-max-workspace-team-members";
        public static NewUserDefaultMaxWorkspaceTeamMembersSetting Default => new(0);

        public static NewUserDefaultMaxWorkspaceTeamMembersSetting FromString(string value)
        {
            if (int.TryParse(value, out var intValue))
            {
                if (intValue == -1)
                    return new(Value: null);

                return new(Value: intValue);
            }

            throw new ArgumentOutOfRangeException(
                $"Setting '{Key}' value was expected to be an int32 but found '{value}'");
        }

        public string Serialize()
        {
            return Value.HasValue
                ? Value.Value.ToString()
                : "-1";
        }
    }

    public record NewUserDefaultPermissionsAndRolesSetting(List<string> permissionsAndRoles)
    {
        public const string Key = "new-user-default-permissions-and-roles";
        public static NewUserDefaultPermissionsAndRolesSetting Default => new([]);

        public static NewUserDefaultPermissionsAndRolesSetting FromString(string value)
        {
            var splitted = value
                .Split(",", StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            foreach (var item in splitted)
            {
                if(!Permissions.IsValidPermission(item) && !Roles.IsValidRole(item))
                {
                    throw new ArgumentOutOfRangeException(
                        $"Setting '{Key}' value contains invalid element: '{value}'. All items are expected to be valid permissions or roles.");
                }
            }

            return new(splitted);
        }

        public string Serialize() => string.Join(",", permissionsAndRoles);

        public bool IsAdmin => permissionsAndRoles.Contains(Roles.Admin);

        public bool CanAddWorkspace => permissionsAndRoles.Contains(Permissions.AddWorkspace);
        public bool CanManageGeneralSettings => permissionsAndRoles.Contains(Permissions.ManageGeneralSettings);
        public bool CanManageUsers => permissionsAndRoles.Contains(Permissions.ManageUsers);
        public bool CanManageStorages => permissionsAndRoles.Contains(Permissions.ManageStorages);
        public bool CanManageEmailProviders => permissionsAndRoles.Contains(Permissions.ManageEmailProviders);
        public List<string> GetPermissions() => permissionsAndRoles.Where(Permissions.IsValidPermission).ToList();
    }

    public record ApplicationNameSetting(string Name)
    {
        public const string Key = "application-name";
        public static ApplicationNameSetting Default => new("PlikShare");
    }
    
    public record PrivacyPolicySetting(string? FileName)
    {
        public const string Key = "privacy-policy";
        public static PrivacyPolicySetting Default => new(FileName: null);
    }
    
    public record TermsOfServiceSetting(string? FileName)
    {
        public const string Key = "terms-of-service";
        public static TermsOfServiceSetting Default => new(FileName: null);
    }
    
    public record SignUpSetting
    {
        public const string Key = "application-sing-up";
        public string Value { get; }
        private SignUpSetting(string value) => Value = value;
        
        public static readonly SignUpSetting OnlyInvitedUsers = new ("only-invited-users");
        public static readonly SignUpSetting Everyone = new("everyone");
        public static SignUpSetting Default => OnlyInvitedUsers;

        public static SignUpSetting FromString(string value)
        {
            if (value == Everyone.Value)
                return Everyone;

            if (value == OnlyInvitedUsers.Value)
                return OnlyInvitedUsers;

            throw new ArgumentOutOfRangeException(
                $"Setting '{Key}' value can be either '{Everyone}' or '{OnlyInvitedUsers}' " +
                $"but found '{value}'");
        }

        public static bool TryParse(string value, out SignUpSetting signUp)
        {
            signUp = null!;

            if (value == Everyone.Value)
            {
                signUp =  Everyone;
                return true;
            }

            if (value == OnlyInvitedUsers.Value)
            {
                signUp = OnlyInvitedUsers;
                return true;
            }

            return false;
        }

        public override string ToString() => Value;
    }

    private volatile List<SignUpCheckbox> _signUpCheckboxes = [];
    public IReadOnlyList<SignUpCheckbox> SignUpCheckboxes => _signUpCheckboxes;
    public IEnumerable<int> RequiredSignUpCheckboxesIds => SignUpCheckboxes.Where(x => x.IsRequired).Select(x => x.Id);
 
    private volatile SignUpSetting _applicationSignUp = SignUpSetting.Default;
    public SignUpSetting ApplicationSignUp => _applicationSignUp;


    private volatile TermsOfServiceSetting _termsOfService = TermsOfServiceSetting.Default;
    public TermsOfServiceSetting TermsOfService => _termsOfService;


    private volatile PrivacyPolicySetting _privacyPolicy = PrivacyPolicySetting.Default;
    public PrivacyPolicySetting PrivacyPolicy => _privacyPolicy;


    private volatile ApplicationNameSetting _applicationName = ApplicationNameSetting.Default;
    public ApplicationNameSetting ApplicationName => _applicationName;


    private volatile NewUserDefaultMaxWorkspaceNumberSetting _newUserDefaultMaxWorkspaceNumber = NewUserDefaultMaxWorkspaceNumberSetting.Default;
    public NewUserDefaultMaxWorkspaceNumberSetting NewUserDefaultMaxWorkspaceNumber => _newUserDefaultMaxWorkspaceNumber;


    private volatile NewUserDefaultMaxWorkspaceSizeInBytesSetting _newUserDefaultMaxWorkspaceSizeInBytes = NewUserDefaultMaxWorkspaceSizeInBytesSetting.Default;
    public NewUserDefaultMaxWorkspaceSizeInBytesSetting NewUserDefaultMaxWorkspaceSizeInBytes => _newUserDefaultMaxWorkspaceSizeInBytes;


    private volatile NewUserDefaultMaxWorkspaceTeamMembersSetting _newUserDefaultMaxWorkspaceTeamMembers = NewUserDefaultMaxWorkspaceTeamMembersSetting.Default;
    public NewUserDefaultMaxWorkspaceTeamMembersSetting NewUserDefaultMaxWorkspaceTeamMembers => _newUserDefaultMaxWorkspaceTeamMembers;


    private volatile NewUserDefaultPermissionsAndRolesSetting _newUserDefaultPermissionsAndRoles = NewUserDefaultPermissionsAndRolesSetting.Default;
    public NewUserDefaultPermissionsAndRolesSetting NewUserDefaultPermissionsAndRoles => _newUserDefaultPermissionsAndRoles;


    private volatile AlertOnNewUserRegisteredSetting _alertOnNewUserRegistered = AlertOnNewUserRegisteredSetting.Default;
    public AlertOnNewUserRegisteredSetting AlertOnNewUserRegistered => _alertOnNewUserRegistered;

    public int AdminRoleId { get; private set; }

    public void SetAlertOnNewUserRegistered(bool isTurnedOn)
    {
        var setting = new AlertOnNewUserRegisteredSetting(isTurnedOn);

        UpdateSettingInDatabase(
            key: AlertOnNewUserRegisteredSetting.Key,
            value: setting.Serialize());

        _alertOnNewUserRegistered = setting;
    }

    public void SetApplicationName(string? name)
    {
        UpdateSettingInDatabase(
            key: ApplicationNameSetting.Key,
            value: name);

        _applicationName = new ApplicationNameSetting(name!);
    }
    
    public void SetPrivacyPolicy(string? fileName)
    {
        UpdateSettingInDatabase(
            key: PrivacyPolicySetting.Key,
            value: fileName);

        _privacyPolicy = new PrivacyPolicySetting(fileName);
    }
    
    public void SetTermsOfService(string? fileName)
    {
        UpdateSettingInDatabase(
            key: TermsOfServiceSetting.Key,
            value: fileName);

        _termsOfService = new TermsOfServiceSetting(fileName);
    }
    
    public void SetApplicationSignUp(SignUpSetting signUp)
    {
        UpdateSettingInDatabase(
            key: SignUpSetting.Key,
            value: signUp.Value);

        _applicationSignUp = signUp;
    }

    public void SetNewUserDefaultMaxWorkspaceNumber(int? value)
    {
        var setting = new NewUserDefaultMaxWorkspaceNumberSetting(value);

        UpdateSettingInDatabase(
            key: NewUserDefaultMaxWorkspaceNumberSetting.Key,
            value: setting.Serialize());

        _newUserDefaultMaxWorkspaceNumber = setting;
    }

    public void SetNewUserDefaultMaxWorkspaceSizeInBytes(long? value)
    {
        var setting = new NewUserDefaultMaxWorkspaceSizeInBytesSetting(value);

        UpdateSettingInDatabase(
            key: NewUserDefaultMaxWorkspaceSizeInBytesSetting.Key,
            value: setting.Serialize());

        _newUserDefaultMaxWorkspaceSizeInBytes = setting;
    }

    public void SetNewUserDefaultMaxWorkspaceTeamMembers(int? value)
    {
        var setting = new NewUserDefaultMaxWorkspaceTeamMembersSetting(value);

        UpdateSettingInDatabase(
            key: NewUserDefaultMaxWorkspaceTeamMembersSetting.Key,
            value: setting.Serialize());

        _newUserDefaultMaxWorkspaceTeamMembers = setting;
    }

    public void SetNewUserPermissionsAndRoles(List<string> permissionsAndRoles)
    {
        var setting = new NewUserDefaultPermissionsAndRolesSetting(permissionsAndRoles);

        UpdateSettingInDatabase(
            key: NewUserDefaultPermissionsAndRolesSetting.Key,
            value: setting.Serialize());

        _newUserDefaultPermissionsAndRoles = setting;
    }

    public void RefreshSingUpCheckboxes()
    {
        using var connection = plikShareDb.OpenConnection();

        _signUpCheckboxes = GetSignUpCheckboxes(connection);
    }
    
    public void Initialize()
    {
        using var connection = plikShareDb.OpenConnection();
        
        _signUpCheckboxes = GetSignUpCheckboxes(connection);
        
        var settings = GetSettings(connection);

        _applicationSignUp = GetApplicationSignUpOrDefault(settings);
        _termsOfService = GetTermsOfServiceOrDefault(settings);
        _privacyPolicy = GetPrivacyPolicyOrDefault(settings);
        _applicationName = GetApplicationNameOrDefault(settings);
        _newUserDefaultMaxWorkspaceNumber = GetNewUserDefaultMaxWorkspaceNumberOrDefault(settings);
        _newUserDefaultMaxWorkspaceSizeInBytes = GetNewUserDefaultMaxWorkspaceSizeInBytesOrDefault(settings);
        _newUserDefaultMaxWorkspaceTeamMembers = GetNewUserDefaultMaxWorkspaceTeamMembersOrDefault(settings);
        _newUserDefaultPermissionsAndRoles = GetNewUserDefaultPermissionsAndRolesOrDefault(settings);
        _alertOnNewUserRegistered = GetAlertOnNewUserRegisteredOrDefault(settings);

        AdminRoleId = GetOrCreateAdminRole(connection);
    }
    
    private static int GetOrCreateAdminRole(
        SqliteConnection connection)
    {
        var adminRoleId = connection
            .OneRowCmd(
                sql: """
                     SELECT r_id
                     FROM r_roles
                     WHERE r_normalized_name = $normalizedName
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$normalizedName", Roles.AdminNormalized)
            .Execute();

        if (!adminRoleId.IsEmpty)
            return adminRoleId.Value;

        return connection
            .OneRowCmd(
                sql: """
                     INSERT INTO r_roles (
                         r_external_id,
                         r_name,
                         r_normalized_name,
                         r_concurrency_stamp
                     ) VALUES (
                         $externalId,
                         $name,
                         $normalizedName,
                         $concurrencyStamp
                     )
                     RETURNING r_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$externalId", RoleExtId.NewId().Value)
            .WithParameter("$name", Roles.Admin)
            .WithParameter("$normalizedName", Roles.AdminNormalized)
            .WithParameter("$concurrencyStamp", Guid.NewGuid())
            .ExecuteOrThrow();
    }

    private static List<Setting> GetSettings(
        SqliteConnection connection)
    {
        var settings = connection
            .Cmd(
                sql: """
                     SELECT as_key, as_value
                     FROM as_app_settings
                     """,
                readRowFunc: reader => new Setting(
                    Key: reader.GetString(0),
                    Value: reader.GetStringOrNull(1)))
            .Execute();
        return settings;
    }

    private List<SignUpCheckbox> GetSignUpCheckboxes(
        SqliteConnection connection)
    {
        return connection
            .Cmd(
                sql: """
                     SELECT
                        suc_id,
                        suc_text,
                        suc_is_required
                     FROM 
                        suc_sign_up_checkboxes
                     WHERE 
                        suc_is_deleted = FALSE
                     """,
                readRowFunc: reader => new SignUpCheckbox
                {
                    Id = reader.GetInt32(0),
                    Text = reader.GetString(1),
                    IsRequired = reader.GetBoolean(2)
                })
            .Execute();
    }

    private SignUpSetting GetApplicationSignUpOrDefault(IEnumerable<Setting> settings)
    {
        var setting = settings.FirstOrDefault(
            s => s.Key.Equals(SignUpSetting.Key));

        return setting?.Value is null
            ? SignUpSetting.Default
            : SignUpSetting.FromString(setting.Value);
    }
    
    private TermsOfServiceSetting GetTermsOfServiceOrDefault(IEnumerable<Setting> settings)
    {
        var setting = settings.FirstOrDefault(
            s => s.Key.Equals(TermsOfServiceSetting.Key));

        return setting is null
            ? TermsOfServiceSetting.Default
            : new TermsOfServiceSetting(setting.Value);
    }
    
    private PrivacyPolicySetting GetPrivacyPolicyOrDefault(IEnumerable<Setting> settings)
    {
        var setting = settings.FirstOrDefault(
            s => s.Key.Equals(PrivacyPolicySetting.Key));

        return setting is null
            ? PrivacyPolicySetting.Default
            : new PrivacyPolicySetting(setting.Value);
    }
    
    private ApplicationNameSetting GetApplicationNameOrDefault(IEnumerable<Setting> settings)
    {
        var setting = settings.FirstOrDefault(
            s => s.Key.Equals(ApplicationNameSetting.Key));

        return setting is null
            ? ApplicationNameSetting.Default
            : new ApplicationNameSetting(setting.Value!);
    }

    private NewUserDefaultMaxWorkspaceNumberSetting GetNewUserDefaultMaxWorkspaceNumberOrDefault(IEnumerable<Setting> settings)
    {
        var setting = settings.FirstOrDefault(
           s => s.Key.Equals(NewUserDefaultMaxWorkspaceNumberSetting.Key));

        return setting is null
            ? NewUserDefaultMaxWorkspaceNumberSetting.Default
            : NewUserDefaultMaxWorkspaceNumberSetting.FromString(setting.Value!);
    }

    private NewUserDefaultMaxWorkspaceSizeInBytesSetting GetNewUserDefaultMaxWorkspaceSizeInBytesOrDefault(IEnumerable<Setting> settings)
    {
        var setting = settings.FirstOrDefault(
           s => s.Key.Equals(NewUserDefaultMaxWorkspaceSizeInBytesSetting.Key));

        return setting is null
            ? NewUserDefaultMaxWorkspaceSizeInBytesSetting.Default
            : NewUserDefaultMaxWorkspaceSizeInBytesSetting.FromString(setting.Value!);
    }

    private NewUserDefaultMaxWorkspaceTeamMembersSetting GetNewUserDefaultMaxWorkspaceTeamMembersOrDefault(IEnumerable<Setting> settings)
    {
        var setting = settings.FirstOrDefault(
            s => s.Key.Equals(NewUserDefaultMaxWorkspaceTeamMembersSetting.Key));

        return setting is null
            ? NewUserDefaultMaxWorkspaceTeamMembersSetting.Default
            : NewUserDefaultMaxWorkspaceTeamMembersSetting.FromString(setting.Value!);
    }

    private NewUserDefaultPermissionsAndRolesSetting GetNewUserDefaultPermissionsAndRolesOrDefault(IEnumerable<Setting> settings)
    {
        var setting = settings.FirstOrDefault(
           s => s.Key.Equals(NewUserDefaultPermissionsAndRolesSetting.Key));

        return setting is null
            ? NewUserDefaultPermissionsAndRolesSetting.Default
            : NewUserDefaultPermissionsAndRolesSetting.FromString(setting.Value!);
    }

    private AlertOnNewUserRegisteredSetting GetAlertOnNewUserRegisteredOrDefault(IEnumerable<Setting> settings)
    {
        var setting = settings.FirstOrDefault(
           s => s.Key.Equals(AlertOnNewUserRegisteredSetting.Key));

        return setting is null
            ? AlertOnNewUserRegisteredSetting.Default
            : AlertOnNewUserRegisteredSetting.FromString(setting.Value!);
    }

    private void UpdateSettingInDatabase(string key, string? value)
    {
        using var connection = plikShareDb.OpenConnection();

        connection
            .OneRowCmd(
                sql: """
                     INSERT INTO as_app_settings(
                         as_key,
                         as_value
                     ) 
                     VALUES (
                         $key,
                         $value
                     )
                     ON CONFLICT (as_key)
                     DO UPDATE SET as_value = excluded.as_value
                     RETURNING as_key
                     """,
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$key", key)
            .WithParameter("$value", value)
            .ExecuteOrThrow();
    }
    
    private record Setting(
        string Key,
        string? Value);
}