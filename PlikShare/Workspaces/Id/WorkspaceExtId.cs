using System.ComponentModel;
using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.Workspaces.Id;

[ImmutableObject(true)]
[JsonConverter(typeof(ExternalIdJsonConverter<WorkspaceExtId>))]
public readonly record struct WorkspaceExtId(string Value): IExternalId<WorkspaceExtId>
{
    public static string Prefix => "w_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(WorkspaceExtId)}");
        
    public static WorkspaceExtId Parse(string value) => new(value);
    public static WorkspaceExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static (WorkspaceExtId, Guid) NewIdWithSourceGuid()
    {
        var guid = Guid.NewGuid();

        return (
            new($"{Prefix}{Guid.NewGuid().ToBase62()}"),
            guid);
    }

    public static WorkspaceExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out WorkspaceExtId result)
    {
        if (s is null)
        {
            result = new WorkspaceExtId();
            return false;   
        }

        result = new WorkspaceExtId(s);
        return true;
    }
    
    public override string ToString() => Value;
}
