using System.ComponentModel;
using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.Files.Id;

[ImmutableObject(true)]
[JsonConverter(typeof(ExternalIdJsonConverter<FileExtId>))]
public readonly record struct FileExtId(string Value): IExternalId<FileExtId>
{
    public static string Prefix => "fi_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(FileExtId)}");
        
    public static FileExtId Parse(string value) => new(value);
    public static FileExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static FileExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out FileExtId result)
    {
        if (s is null)
        {
            result = new FileExtId();
            return false;   
        }

        result = new FileExtId(s);
        return true;
    }
    
    public override string ToString() => Value;
}