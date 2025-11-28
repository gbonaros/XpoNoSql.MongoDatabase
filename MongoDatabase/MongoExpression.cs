// Part of the XpoNoSql.MongoDatabase provider.
// This file implements a lightweight expression wrapper to annotate MongoDB expressions with source metadata as part of the XPO → MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using DevExpress.Data.Filtering;

using MongoDB.Bson;

using System;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Wraps a MongoDB Bson expression along with metadata about its origin (field/constant/let).
/// </summary>
public readonly struct MongoExpression
{
    public BsonValue Value { get; }

    public bool IsField { get; }

    public bool IsConstant { get; }

    public bool IsLetReference => IsField && Value.IsString && Value.AsString.StartsWith("$$", StringComparison.Ordinal);

    /// <summary>
    /// Initializes a new <see cref="MongoExpression"/> with explicit metadata flags.
    /// </summary>
    /// <param name="value">Underlying MongoDB expression value.</param>
    /// <param name="isField">Indicates the value represents a field reference.</param>
    /// <param name="isConstant">Indicates the value represents a constant.</param>
    public MongoExpression(BsonValue value, bool isField, bool isConstant)
    {
        Value = value ?? BsonNull.Value;
        IsField = isField;
        IsConstant = isConstant;
    }

    /// <summary>
    /// Creates an expression representing a Mongo field reference.
    /// </summary>
    public static MongoExpression Field(string path)
    {
        return new MongoExpression(BsonValue.Create(path), isField: true, isConstant: false);
    }

    /// <summary>
    /// Creates an expression representing a constant (including null and Guid handling).
    /// </summary>
    public static MongoExpression Constant(object value)
    {
        if (value is null || value is NullValue)
        {
            return new MongoExpression(BsonNull.Value, isField: false, isConstant: true);
        }

        if (value is Guid guid)
        {
            return new MongoExpression(new BsonBinaryData(guid, GuidRepresentation.Standard), isField: false, isConstant: true);
        }

        if (value is Guid?)
        {
            var nullableGuid = (Guid?)value;
            if (nullableGuid.HasValue)
            {
                return new MongoExpression(new BsonBinaryData(nullableGuid.Value, GuidRepresentation.Standard), isField: false, isConstant: true);
            }

            return new MongoExpression(BsonNull.Value, isField: false, isConstant: true);
        }

        return new MongoExpression(BsonValue.Create(value), isField: false, isConstant: true);
    }

    /// <summary>
    /// Creates an expression wrapping an arbitrary BsonValue.
    /// </summary>
    public static MongoExpression Raw(BsonValue value)
    {
        return new MongoExpression(value, isField: false, isConstant: false);
    }

    /// <summary>
    /// Gets the field name (without leading '$') for field expressions.
    /// </summary>
    public string GetFieldName()
    {
        if (!IsField)
        {
            throw new InvalidOperationException("Expression does not represent a field.");
        }

        return Value.AsString.TrimStart('$');
    }
}

