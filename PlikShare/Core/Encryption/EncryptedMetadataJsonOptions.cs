using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PlikShare.Core.Utils;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Builds and caches <see cref="JsonSerializerOptions"/> used when deserializing SQL-produced
/// JSON aggregates that contain encrypted metadata fields. Each instance is seeded from the
/// shared <see cref="Json.Options"/> (camelCase policy + all registered converters), then
/// wired with a <see cref="DefaultJsonTypeInfoResolver"/> modifier that installs a stateful
/// <see cref="EncryptedMetadataJsonConverter"/> onto every property annotated with
/// <see cref="EncryptedMetadataAttribute"/>.
///
/// Caching strategy:
///   - For a null session (non-encrypted workspace) a single static instance is reused —
///     the converter closes over null and acts as a passthrough.
///   - For each non-null session a per-session options instance is cached via
///     <see cref="ConditionalWeakTable{TKey,TValue}"/>; when the session is disposed and
///     becomes GC-eligible at request end, its options entry is cleared automatically.
/// </summary>
public static class EncryptedMetadataJsonOptions
{
    private static readonly JsonSerializerOptions NoSessionOptions = BuildOptions(session: null);

    private static readonly ConditionalWeakTable<WorkspaceEncryptionSession, JsonSerializerOptions> Cache = new();

    public static JsonSerializerOptions ForSession(WorkspaceEncryptionSession? session)
    {
        if (session is null)
            return NoSessionOptions;

        return Cache.GetValue(session, static s => BuildOptions(s));
    }

    private static JsonSerializerOptions BuildOptions(WorkspaceEncryptionSession? session)
    {
        var converter = new EncryptedMetadataJsonConverter(session);

        var options = new JsonSerializerOptions(Json.Options)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    typeInfo =>
                    {
                        if (typeInfo.Kind != JsonTypeInfoKind.Object)
                            return;

                        foreach (var prop in typeInfo.Properties)
                        {
                            var hasMarker = prop.AttributeProvider?.IsDefined(
                                attributeType: typeof(EncryptedMetadataAttribute),
                                inherit: false) ?? false;

                            if (hasMarker)
                                prop.CustomConverter = converter;
                        }
                    }
                }
            }
        };

        return options;
    }
}
