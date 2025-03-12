using System.ComponentModel;
using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.Storages.Id;

[ImmutableObject(true)]
[JsonConverter(typeof(ExternalIdJsonConverter<StorageExtId>))]
public readonly record struct StorageExtId(string Value): IExternalId<StorageExtId>
{
    public static string Prefix => "s_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(StorageExtId)}");
        
    public static StorageExtId Parse(string value) => new(value);
    public static StorageExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static StorageExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out StorageExtId result)
    {
        if (s is null)
        {
            result = new StorageExtId();
            return false;   
        }

        result = new StorageExtId(s);
        return true;
    }
    
    public override string ToString() => Value;
}
