using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;
using PlikShare.Core.Utils;

namespace PlikShare.Core.ExternalIds;


//todo: that is for now not needed, however not sure if it is because of the fact that in memory it stores objects
//which are marked as immutable, or due to new way of serializing (code generation vs reflection)
internal sealed class PlikShareJsonHybridCacheSerializerFactory : IHybridCacheSerializerFactory
{
    public bool TryCreateSerializer<T>([NotNullWhen(true)] out IHybridCacheSerializer<T>? serializer)
    {
        serializer = new PlikShareJsonSerializer<T>();
        return true;
    }

    internal sealed class PlikShareJsonSerializer<T> : IHybridCacheSerializer<T>
    {
        T IHybridCacheSerializer<T>.Deserialize(ReadOnlySequence<byte> source)
        {
            var reader = new Utf8JsonReader(source);
            return JsonSerializer.Deserialize<T>(ref reader, Json.Options)!;
        }

        void IHybridCacheSerializer<T>.Serialize(T value, IBufferWriter<byte> target)
        {
            using var writer = new Utf8JsonWriter(target);
            JsonSerializer.Serialize(writer, value, Json.Options);
        }
    }
}