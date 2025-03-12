using System.ComponentModel;
using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.Uploads.Id;

[ImmutableObject(true)]
[JsonConverter(typeof(ExternalIdJsonConverter<BulkFileUploadExtId>))]
public readonly record struct BulkFileUploadExtId(string Value) : IExternalId<BulkFileUploadExtId>
{
    public static string Prefix => "bfu_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(BulkFileUploadExtId)}");

    public static BulkFileUploadExtId Parse(string value) => new(value);
    public static BulkFileUploadExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static BulkFileUploadExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out BulkFileUploadExtId result)
    {
        if (s is null)
        {
            result = new BulkFileUploadExtId();
            return false;
        }

        result = new BulkFileUploadExtId(s);
        return true;
    }

    public override string ToString() => Value;
}