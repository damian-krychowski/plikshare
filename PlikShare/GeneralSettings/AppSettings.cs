using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
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