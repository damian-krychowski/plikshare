using System.ComponentModel;
using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.Folders.Id;

[ImmutableObject(true)]
[JsonConverter(typeof(ExternalIdJsonConverter<FolderExtId>))]
public readonly record struct FolderExtId(string Value): IExternalId<FolderExtId>
{
    public static string Prefix => "fo_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(FolderExtId)}");
        
    public static FolderExtId Parse(string value) => new(value);
    public static FolderExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static FolderExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out FolderExtId result)
    {
        if (s is null)
        {
            result = new FolderExtId();
            return false;   
        }

        result = new FolderExtId(s);
        return true;
    }
    
    public override string ToString() => Value;
}