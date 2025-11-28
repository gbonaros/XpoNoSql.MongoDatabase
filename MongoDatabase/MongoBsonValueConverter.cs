// Part of the XpoNoSql.MongoDatabase provider.
// This file implements BSON-to-CLR value conversion helpers for materialization as part of the XPO → MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using DevExpress.Xpo.DB;

using MongoDB.Bson;

using System;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Converts MongoDB BsonValue instances to CLR objects based on XPO DBColumnType information.
/// Used during result materialization and identity generation.
/// </summary>
internal static class MongoBsonValueConverter
{
    private static readonly Dictionary<DBColumnType, Type> ColumnTypeMap = new Dictionary<DBColumnType, Type>
    {
        { DBColumnType.Boolean, typeof(bool) },
        { DBColumnType.Byte, typeof(byte) },
        { DBColumnType.SByte, typeof(sbyte) },
        { DBColumnType.Char, typeof(char) },
        { DBColumnType.Decimal, typeof(decimal) },
        { DBColumnType.Double, typeof(double) },
        { DBColumnType.Single, typeof(float) },
        { DBColumnType.Int32, typeof(int) },
        { DBColumnType.UInt32, typeof(uint) },
        { DBColumnType.Int16, typeof(short) },
        { DBColumnType.UInt16, typeof(ushort) },
        { DBColumnType.Int64, typeof(long) },
        { DBColumnType.UInt64, typeof(ulong) },
        { DBColumnType.String, typeof(string) },
        { DBColumnType.DateTime, typeof(DateTime) },
        { DBColumnType.Guid, typeof(Guid) },
        { DBColumnType.TimeSpan, typeof(TimeSpan) },
        { DBColumnType.ByteArray, typeof(byte[]) },
        { DBColumnType.DateOnly, typeof(DateOnly) },
        { DBColumnType.TimeOnly, typeof(TimeOnly) }
    };

    /// <summary>
    /// Converts a BsonValue to the requested DBColumnType. Falls back to loose conversion on failure.
    /// </summary>
    public static object Convert(BsonValue value, DBColumnType targetType)
    {
        if (value == null || value.IsBsonNull)
        {
            return null;
        }

        if (!ColumnTypeMap.TryGetValue(targetType, out var targetClrType))
        {
            return ConvertLoose(value);
        }

        try
        {
            return ConvertExact(value, targetClrType);
        }
        catch
        {
            return ConvertLoose(value);
        }
    }

    /// <summary>
    /// Basic conversion when no explicit target type is provided or conversion fails; favors native types.
    /// </summary>
    private static object ConvertLoose(BsonValue value)
    {
        switch (value.BsonType)
        {
            case BsonType.Boolean:
                return value.AsBoolean;
            case BsonType.Int32:
                return value.AsInt32;
            case BsonType.Int64:
                return value.AsInt64;
            case BsonType.Double:
                return value.AsDouble;
            case BsonType.Decimal128:
                return (decimal)value.AsDecimal128;
            case BsonType.String:
                return value.AsString;
            case BsonType.DateTime:
                return value.ToUniversalTime();
            case BsonType.Timestamp:
                return value.AsBsonTimestamp;
            case BsonType.ObjectId:
                return value.AsObjectId.ToString();
            case BsonType.Binary:
                return value.AsByteArray;
            case BsonType.Document:
                return value.AsBsonDocument;
            case BsonType.Array:
                {
                    var array = value.AsBsonArray;
                    var result = new object[array.Count];
                    for (int i = 0; i < array.Count; i++)
                    {
                        result[i] = ConvertLoose(array[i]);
                    }

                    return result;
                }
            default:
                return value;
        }
    }

    /// <summary>
    /// Performs a targeted conversion to the exact CLR type for the given DBColumnType.
    /// </summary>
    private static object ConvertExact(BsonValue value, Type targetType)
    {
        if (targetType == typeof(bool))
        {
            if (value.IsBoolean)
            {
                return value.AsBoolean;
            }

            switch (value.BsonType)
            {
                case BsonType.Int32:
                case BsonType.Int64:
                case BsonType.Double:
                case BsonType.Decimal128:
                    return Math.Abs(value.ToDouble()) > double.Epsilon;
            }
            if (value.IsString && bool.TryParse(value.AsString, out var boolParsed))
            {
                return boolParsed;
            }

            return false;
        }

        if (targetType == typeof(byte))
        {
            return (byte)value.ToInt64();
        }

        if (targetType == typeof(sbyte))
        {
            return (sbyte)value.ToInt64();
        }

        if (targetType == typeof(char))
        {
            var str = value.ToString();
            return str.Length > 0 ? str[0] : '\0';
        }

        if (targetType == typeof(decimal))
        {
            if (value.IsDecimal128)
            {
                return (decimal)value.AsDecimal128;
            }

            return System.Convert.ToDecimal(value.ToDouble(), System.Globalization.CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(double))
        {
            return value.ToDouble();
        }

        if (targetType == typeof(float))
        {
            return (float)value.ToDouble();
        }

        if (targetType == typeof(int))
        {
            return value.ToInt32();
        }

        if (targetType == typeof(uint))
        {
            return (uint)value.ToInt64();
        }

        if (targetType == typeof(short))
        {
            return (short)value.ToInt64();
        }

        if (targetType == typeof(ushort))
        {
            return (ushort)value.ToInt64();
        }

        if (targetType == typeof(long))
        {
            return value.ToInt64();
        }

        if (targetType == typeof(ulong))
        {
            if (value.IsDecimal128)
            {
                return (ulong)(decimal)value.AsDecimal128;
            }

            return (ulong)value.ToDouble();
        }

        if (targetType == typeof(string))
        {
            return value.ToString();
        }

        if (targetType == typeof(DateTime))
        {
            if (value.IsValidDateTime)
            {
                return value.ToUniversalTime();
            }

            if (value.IsString && DateTime.TryParse(value.AsString, out var parsed))
            {
                return parsed;
            }

            return System.Convert.ToDateTime(ConvertLoose(value));
        }

        if (targetType == typeof(Guid))
        {
            if (value.IsString && Guid.TryParse(value.AsString, out var g))
            {
                return g;
            }

            if (value.IsBsonBinaryData)
            {
                return value.AsGuid;
            }

            return Guid.Parse(value.ToString());
        }

        if (targetType == typeof(TimeSpan))
        {
            if (value.IsInt64 || value.IsInt32)
            {
                return TimeSpan.FromTicks(value.ToInt64());
            }

            if (value.IsString && TimeSpan.TryParse(value.AsString, out var ts))
            {
                return ts;
            }

            return TimeSpan.Zero;
        }

        if (targetType == typeof(byte[]))
        {
            if (value.IsBsonBinaryData)
            {
                return value.AsByteArray;
            }

            return null;
        }

        if (targetType == typeof(DateOnly))
        {
            if (value.IsValidDateTime)
            {
                return DateOnly.FromDateTime(value.ToUniversalTime());
            }

            if (value.IsString && DateTime.TryParse(value.AsString, out var parsed))
            {
                return DateOnly.FromDateTime(parsed);
            }

            return DateOnly.FromDateTime(DateTime.MinValue);
        }

        if (targetType == typeof(TimeOnly))
        {
            if (value.IsValidDateTime)
            {
                return TimeOnly.FromDateTime(value.ToUniversalTime());
            }

            if (value.IsString && DateTime.TryParse(value.AsString, out var parsed))
            {
                return TimeOnly.FromDateTime(parsed);
            }

            return TimeOnly.MinValue;
        }

        return ConvertLoose(value);
    }

    /// <summary>
    /// Attempts to infer a DBColumnType from a CLR value; used when explicit metadata is missing.
    /// </summary>
    public static DBColumnType DetermineColumnType(object value)
    {
        if (value == null)
        {
            return DBColumnType.Unknown;
        }

        var type = value.GetType();
        if (type == typeof(bool)) return DBColumnType.Boolean;
        if (type == typeof(byte)) return DBColumnType.Byte;
        if (type == typeof(sbyte)) return DBColumnType.SByte;
        if (type == typeof(char)) return DBColumnType.Char;
        if (type == typeof(decimal)) return DBColumnType.Decimal;
        if (type == typeof(double)) return DBColumnType.Double;
        if (type == typeof(float)) return DBColumnType.Single;
        if (type == typeof(int)) return DBColumnType.Int32;
        if (type == typeof(uint)) return DBColumnType.UInt32;
        if (type == typeof(short)) return DBColumnType.Int16;
        if (type == typeof(ushort)) return DBColumnType.UInt16;
        if (type == typeof(long)) return DBColumnType.Int64;
        if (type == typeof(ulong)) return DBColumnType.UInt64;
        if (type == typeof(string)) return DBColumnType.String;
        if (type == typeof(DateTime)) return DBColumnType.DateTime;
        if (type == typeof(Guid)) return DBColumnType.Guid;
        if (type == typeof(TimeSpan)) return DBColumnType.TimeSpan;
        if (type == typeof(byte[])) return DBColumnType.ByteArray;
#if NET6_0_OR_GREATER
        if (type == typeof(DateOnly)) return DBColumnType.DateOnly;
        if (type == typeof(TimeOnly)) return DBColumnType.TimeOnly;
#endif
        return DBColumnType.Unknown;
    }
}

