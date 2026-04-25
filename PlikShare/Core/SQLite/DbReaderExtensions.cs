using System.Data.Common;
using System.Text;
using PlikShare.Core.Encryption;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;
using PlikShare.Users.Entities;

namespace PlikShare.Core.SQLite;

public static class DbReaderExtensions
{
    public static string DecodeEncryptableString(
        this DbDataReader reader,
        int ordinal,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        var value = reader.GetString(ordinal);

        return workspaceEncryptionSession.DecodeEncryptableMetadata(
            encoded: value);
    }

    public static string? DecodeEncryptableStringOrNull(
        this DbDataReader reader,
        int ordinal,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        if (reader.IsDBNull(ordinal))
            return null;

        var value = reader.GetString(ordinal);

        return workspaceEncryptionSession.DecodeEncryptableMetadata(
            encoded: value);
    }

    /// <summary>
    /// BLOB-affinity sibling of <see cref="DecodeEncryptableString"/>: reads the cell as
    /// UTF-8 bytes, interprets them as a string, then decodes through the workspace
    /// encryption session. Use for columns declared BLOB whose writers go through
    /// <see cref="SQLiteOneRowCommandExecutor{TRow}.WithEncryptableBlobParameter"/>.
    /// </summary>
    public static string DecodeEncryptableBlob(
        this DbDataReader reader,
        int ordinal,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        var bytes = reader.GetFieldValue<byte[]>(ordinal);
        var value = Encoding.UTF8.GetString(bytes);

        return workspaceEncryptionSession.DecodeEncryptableMetadata(
            encoded: value);
    }

    /// <summary>
    /// Nullable variant of <see cref="DecodeEncryptableBlob"/> for nullable BLOB columns.
    /// Returns null when the cell is NULL; otherwise behaves identically.
    /// </summary>
    public static string? DecodeEncryptableBlobOrNull(
        this DbDataReader reader,
        int ordinal,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        if (reader.IsDBNull(ordinal))
            return null;

        var bytes = reader.GetFieldValue<byte[]>(ordinal);
        var value = Encoding.UTF8.GetString(bytes);

        return workspaceEncryptionSession.DecodeEncryptableMetadata(
            encoded: value);
    }

    public static EncodedMetadataValue GetEncodedMetadata(
        this DbDataReader reader,
        int ordinal)
    {
        return new EncodedMetadataValue(reader.GetString(ordinal));
    }

    public static EncodedMetadataValue? GetEncodedMetadataOrNull(
        this DbDataReader reader,
        int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? null
            : new EncodedMetadataValue(reader.GetString(ordinal));
    }

    public static Email GetEmail(
        this DbDataReader reader, 
        int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader);
        
        var str = reader.GetString(ordinal);
        return new Email(str);
    }
    
    public static TExtId GetExtId<TExtId>(
        this DbDataReader reader, 
        int ordinal) where TExtId: IExternalId<TExtId>
    {
        ArgumentNullException.ThrowIfNull(reader);
        
        var str = reader.GetString(ordinal);
        return TExtId.Parse(str, null);
    }
    
    public static TExtId? GetExtIdOrNull<TExtId>(
        this DbDataReader reader, 
        int ordinal) where TExtId:struct, IExternalId<TExtId>
    {
        ArgumentNullException.ThrowIfNull(reader);
        
        return reader.IsDBNull(ordinal)
            ? null
            : TExtId.Parse(reader.GetString(ordinal), null);
    }
    
    public static int? GetInt32OrNull(this DbDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader);
        
        return reader.IsDBNull(ordinal) 
            ? null 
            : reader.GetInt32(ordinal);
    }

    public static long? GetInt64OrNull(this DbDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetInt64(ordinal);
    }

    public static T GetFromJson<T>(this DbDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var stringValue = reader.GetString(ordinal);
        var deserialized = Json.Deserialize<T>(stringValue);

        if (deserialized is null)
        {
            throw new InvalidOperationException(
                $"Deserialized json value from '{stringValue}' is null.");
        }

        return deserialized;
    }

    public static T? GetFromJsonOrNull<T>(this DbDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var stringValue = reader.GetStringOrNull(ordinal);

        if (stringValue is null)
            return default;

        var deserialized = Json.Deserialize<T>(stringValue);

        if (deserialized is null)
        {
            throw new InvalidOperationException(
                $"Deserialized json value from '{stringValue}' is null.");
        }

        return deserialized;
    }

    public static TEnum GetEnum<TEnum>(this DbDataReader reader, int ordinal) where TEnum: struct, Enum
    {
        ArgumentNullException.ThrowIfNull(reader);

        var stringValue = reader.GetString(ordinal);
        var deserialized = EnumUtils.FromKebabCase<TEnum>(stringValue);

        return deserialized;
    }

    public static string GetStringFromBlob(this DbDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader);
        
        var bytes = reader.GetFieldValue<byte[]>(ordinal);

        return Encoding.UTF8.GetString(bytes);
    }

    public static string? GetStringOrNull(this DbDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader);
        
        return reader.IsDBNull(ordinal) 
            ? null 
            : reader.GetString(ordinal);
    }

    public static byte? GetByteOrNull(this DbDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetByte(ordinal);
    }

    public static T? GetFieldValueOrNull<T>(this DbDataReader reader, int ordinal) where T : class
    {
        ArgumentNullException.ThrowIfNull(reader);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetFieldValue<T>(ordinal);
    }
    
    public static DateTimeOffset? GetDateTimeOffsetOrNull(this DbDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader);
        
        return reader.IsDBNull(ordinal) 
            ? null 
            : reader.GetFieldValue<DateTimeOffset>(ordinal);
    }
}