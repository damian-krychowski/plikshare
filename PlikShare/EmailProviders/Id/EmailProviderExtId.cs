using System.ComponentModel;
using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.EmailProviders.Id;

[ImmutableObject(true)]
[JsonConverter(typeof(ExternalIdJsonConverter<EmailProviderExtId>))]
public readonly record struct EmailProviderExtId(string Value): IExternalId<EmailProviderExtId>
{
    public static string Prefix => "ep_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(EmailProviderExtId)}");
        
    public static EmailProviderExtId Parse(string value) => new(value);
    public static EmailProviderExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static EmailProviderExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out EmailProviderExtId result)
    {
        if (s is null)
        {
            result = new EmailProviderExtId();
            return false;   
        }

        result = new EmailProviderExtId(s);
        return true;
    }
    
    public override string ToString() => Value;
}
