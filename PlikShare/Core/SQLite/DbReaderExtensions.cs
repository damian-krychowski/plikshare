using System.Data.Common;
using System.Text;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;
using PlikShare.Users.Entities;

namespace PlikShare.Core.SQLite;

public static class DbReaderExtensions
{
    public static TExtId[] GetExtIds<TExtId>(
        this DbDataReader reader, 
        int ordinal) where TExtId: IExternalId<TExtId>
    {
        ArgumentNullException.ThrowIfNull(reader, nameof (reader));

        var strings = reader.GetFieldValue<string[]>(ordinal);

        return strings
            .Select(x => TExtId.Parse(x, null))
            .ToArray();
    }
    
    public static Email GetEmail(
        this DbDataReader reader, 
        int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof (reader));
        
        var str = reader.GetString(ordinal);
        return new Email(str);
    }

    
    public static TExtId GetExtId<TExtId>(
        this DbDataReader reader, 
        int ordinal) where TExtId: IExternalId<TExtId>
    {
        ArgumentNullException.ThrowIfNull(reader, nameof (reader));
        
        var str = reader.GetString(ordinal);
        return TExtId.Parse(str, null);
    }
    
    public static TExtId GetExtId<TExtId>(
        this DbDataReader reader, 
        string name) where TExtId: IExternalId<TExtId>
    {
        ArgumentNullException.ThrowIfNull(reader, nameof (reader));

        var ordinal = reader.GetOrdinal(name);
        var str = reader.GetString(ordinal);
        return TExtId.Parse(str, null);
    }
    
    public static TExtId? GetExtIdOrNull<TExtId>(
        this DbDataReader reader, 
        string name) where TExtId:struct, IExternalId<TExtId>
    {
        ArgumentNullException.ThrowIfNull(reader, nameof (reader));
        
        var ordinal = reader.GetOrdinal(name);

        return reader.IsDBNull(ordinal)
            ? null
            : TExtId.Parse(reader.GetString(ordinal), null);
    }
    
    public static TExtId? GetExtIdOrNull<TExtId>(
        this DbDataReader reader, 
        int ordinal) where TExtId:struct, IExternalId<TExtId>
    {
        ArgumentNullException.ThrowIfNull(reader, nameof (reader));
        
        return reader.IsDBNull(ordinal)
            ? null
            : TExtId.Parse(reader.GetString(ordinal), null);
    }
    
    public static int? GetInt32OrNull(this DbDataReader reader, string name)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof (reader));
        
        var ordinal = reader.GetOrdinal(name);
        
        return reader.IsDBNull(ordinal) 
            ? null 
            : reader.GetInt32(ordinal);
    }
    
    public static int? GetInt32OrNull(this DbDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof (reader));
        
        return reader.IsDBNull(ordinal) 
            ? null 
            : reader.GetInt32(ordinal);
    }

    public static long? GetInt64OrNull(this DbDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof(reader));

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetInt64(ordinal);
    }

    public static T GetFromJson<T>(this DbDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof (reader));

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
        ArgumentNullException.ThrowIfNull(reader, nameof(reader));

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
        ArgumentNullException.ThrowIfNull(reader, nameof(reader));

        var stringValue = reader.GetString(ordinal);
        var deserialized = EnumUtils.FromKebabCase<TEnum>(stringValue);

        return deserialized;
    }

    public static string? GetStringOrNull(this DbDataReader reader, string name)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof (reader));
        
        var ordinal = reader.GetOrdinal(name);
        
        return reader.IsDBNull(ordinal) 
            ? null 
            : reader.GetString(ordinal);
    }

    public static string GetStringFromBlob(this DbDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof(reader));
        
        var bytes = reader.GetFieldValue<byte[]>(ordinal);

        return Encoding.UTF8.GetString(bytes);
    }


    public static string? GetStringOrNullFromBlob(this DbDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof(reader));

        if (reader.IsDBNull(ordinal))
            return null;

        var bytes = reader.GetFieldValue<byte[]>(ordinal);

        return Encoding.UTF8.GetString(bytes);
    }

    public static string? GetStringOrNull(this DbDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof (reader));
        
        return reader.IsDBNull(ordinal) 
            ? null 
            : reader.GetString(ordinal);
    }

    public static byte? GetByteOrNull(this DbDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof(reader));

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetByte(ordinal);
    }

    public static T? GetFieldValueOrNull<T>(this DbDataReader reader, int ordinal) where T : class
    {
        ArgumentNullException.ThrowIfNull(reader, nameof(reader));

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetFieldValue<T>(ordinal);
    }

    public static bool TryGetString(this DbDataReader reader, string name, out string value)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof (reader));
        
        var ordinal = reader.GetOrdinal(name);

        if (reader.IsDBNull(ordinal))
        {
            value = null!;
            return false;
        }

        value = reader.GetString(ordinal);
        return true;
    }
    
    public static bool TryGetString(this DbDataReader reader, int ordinal, out string value)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof (reader));
        
        if (reader.IsDBNull(ordinal))
        {
            value = null!;
            return false;
        }

        value = reader.GetString(ordinal);
        return true;
    }
    
    public static bool TryGetDateTimeOffset(this DbDataReader reader, string name, out DateTimeOffset value)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof(reader));

        var ordinal = reader.GetOrdinal(name);

        if (reader.IsDBNull(ordinal))
        {
            value = default;
            return false;
        }

        value = reader.GetFieldValue<DateTimeOffset>(ordinal);
        return true;
    }
    
    public static bool TryGetDateTimeOffset(this DbDataReader reader, int ordinal, out DateTimeOffset value)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof(reader));

        if (reader.IsDBNull(ordinal))
        {
            value = default;
            return false;
        }

        value = reader.GetFieldValue<DateTimeOffset>(ordinal);
        return true;
    }
    
    public static bool TryGetInt64(this DbDataReader reader, string name, out long value)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof(reader));

        var ordinal = reader.GetOrdinal(name);

        if (reader.IsDBNull(ordinal))
        {
            value = default;
            return false;
        }

        value = reader.GetInt64(ordinal);
        return true;
    }
    
    public static bool TryGetInt64(this DbDataReader reader, int ordinal, out long value)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof(reader));

        if (reader.IsDBNull(ordinal))
        {
            value = default;
            return false;
        }

        value = reader.GetInt64(ordinal);
        return true;
    }
    
    public static bool TryGetInt32(this DbDataReader reader, string name, out int value)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof(reader));

        var ordinal = reader.GetOrdinal(name);

        if (reader.IsDBNull(ordinal))
        {
            value = default;
            return false;
        }

        value = reader.GetInt32(ordinal);
        return true;
    }
    
    public static bool TryGetInt32(this DbDataReader reader, int ordinal, out int value)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof(reader));

        if (reader.IsDBNull(ordinal))
        {
            value = default;
            return false;
        }

        value = reader.GetInt32(ordinal);
        return true;
    }

    public static bool TryGetInt16(this DbDataReader reader, string name, out short value)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof(reader));

        var ordinal = reader.GetOrdinal(name);

        if (reader.IsDBNull(ordinal))
        {
            value = default;
            return false;
        }

        value = reader.GetInt16(ordinal);
        return true;
    }
    
    public static bool TryGetInt16(this DbDataReader reader, int ordinal, out short value)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof(reader));

        if (reader.IsDBNull(ordinal))
        {
            value = default;
            return false;
        }

        value = reader.GetInt16(ordinal);
        return true;
    }

    public static bool TryGetBoolean(this DbDataReader reader, string name, out bool value)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof(reader));

        var ordinal = reader.GetOrdinal(name);

        if (reader.IsDBNull(ordinal))
        {
            value = default;
            return false;
        }

        value = reader.GetBoolean(ordinal);
        return true;
    }
    
    public static bool TryGetBoolean(this DbDataReader reader, int ordinal, out bool value)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof(reader));

        if (reader.IsDBNull(ordinal))
        {
            value = default;
            return false;
        }

        value = reader.GetBoolean(ordinal);
        return true;
    }
    
    public static DateTimeOffset? GetDateTimeOffsetOrNull(this DbDataReader reader, string name)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof (reader));
        
        var ordinal = reader.GetOrdinal(name);
        
        return reader.IsDBNull(ordinal) 
            ? null 
            : reader.GetFieldValue<DateTimeOffset>(ordinal);
    }
    
    public static DateTimeOffset? GetDateTimeOffsetOrNull(this DbDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof (reader));
        
        return reader.IsDBNull(ordinal) 
            ? null 
            : reader.GetFieldValue<DateTimeOffset>(ordinal);
    }
}