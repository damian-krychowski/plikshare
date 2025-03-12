using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlikShare.Core.ExternalIds;

public class ExternalIdJsonConverter<TId> : JsonConverter<TId> where TId: IExternalId<TId>
{
    public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return TId.Parse(reader.GetString()!, null);
    }

    public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}