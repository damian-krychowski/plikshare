using System.ComponentModel;
using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.Files.Id;

[ImmutableObject(true)]
[JsonConverter(typeof(ExternalIdJsonConverter<FileArtifactExtId>))]
public readonly record struct FileArtifactExtId(string Value) : IExternalId<FileArtifactExtId>
{
    public static string Prefix => "fa_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(FileArtifactExtId)}");

    public static FileArtifactExtId Parse(string value) => new(value);
    public static FileArtifactExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static FileArtifactExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out FileArtifactExtId result)
    {
        if (s is null)
        {
            result = new FileArtifactExtId();
            return false;
        }

        result = new FileArtifactExtId(s);
        return true;
    }

    public override string ToString() => Value;
}