using System.Data.Common;
using System.Text;
using System.Text.Json;

namespace PlikShare.Core.Encryption;

/// <summary>
/// <c>DbDataReader</c> overloads that deserialize SQL-produced JSON aggregates with session-aware
/// decryption of every string property marked <see cref="EncryptedMetadataAttribute"/>.
///
/// Mirrors the non-encryption overloads in <c>DbReaderExtensions</c>; the only behavioural
/// difference is the <see cref="WorkspaceEncryptionSession"/> parameter flowing into the
/// per-session <see cref="JsonSerializerOptions"/> obtained from
/// <see cref="EncryptedMetadataJsonOptions.ForSession"/>.
/// </summary>
public static class EncryptableMetadataReaderExtensions
{
    public static T GetFromJson<T>(
        this DbDataReader reader,
        int ordinal,
        WorkspaceEncryptionSession? session)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var stringValue = reader.GetString(ordinal);
        var options = EncryptedMetadataJsonOptions.ForSession(session);
        var deserialized = JsonSerializer.Deserialize<T>(
            utf8Json: Encoding.UTF8.GetBytes(stringValue),
            options: options);

        if (deserialized is null)
        {
            throw new InvalidOperationException(
                $"Deserialized json value from '{stringValue}' is null.");
        }

        return deserialized;
    }

    public static T? GetFromJsonOrNull<T>(
        this DbDataReader reader,
        int ordinal,
        WorkspaceEncryptionSession? session)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (reader.IsDBNull(ordinal))
            return default;

        var stringValue = reader.GetString(ordinal);
        var options = EncryptedMetadataJsonOptions.ForSession(session);
        var deserialized = JsonSerializer.Deserialize<T>(
            utf8Json: Encoding.UTF8.GetBytes(stringValue),
            options: options);

        if (deserialized is null)
        {
            throw new InvalidOperationException(
                $"Deserialized json value from '{stringValue}' is null.");
        }

        return deserialized;
    }
}
