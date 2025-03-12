using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlikShare.Core.Utils;

public class NullableByteArrayJsonConverter : JsonConverter<byte[]>
{
    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<byte>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                if (reader.TokenType == JsonTokenType.Number)
                    list.Add(reader.GetByte());
            }
            return list.ToArray();
        }

        throw new JsonException("Unexpected token type when parsing byte array");
    }

    public override void Write(Utf8JsonWriter writer, byte[]? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (byte b in value)
        {
            writer.WriteNumberValue(b);
        }
        writer.WriteEndArray();
    }
}