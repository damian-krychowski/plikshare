using System.ComponentModel;
using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.Uploads.Id;

[ImmutableObject(true)]
[JsonConverter(typeof(ExternalIdJsonConverter<FileUploadExtId>))]
public readonly record struct FileUploadExtId(string Value): IExternalId<FileUploadExtId>
{
    public static string Prefix => "fu_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(FileUploadExtId)}");
        
    public static FileUploadExtId Parse(string value) => new(value);
    public static FileUploadExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static FileUploadExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out FileUploadExtId result)
    {
        if (s is null)
        {
            result = new FileUploadExtId();
            return false;   
        }

        result = new FileUploadExtId(s);
        return true;
    }
    
    public override string ToString() => Value;
}