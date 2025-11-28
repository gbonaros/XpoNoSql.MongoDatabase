// Part of the XpoNoSql.MongoDatabase provider.
// This file implements internal aggregate binding metadata for grouped queries as part of the XPO → MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;

using System;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Represents an aggregate binding with source container, alias, and aggregate type.
/// </summary>
public sealed class MongoAggregateBinding
{
    /// <summary>
    /// Source aggregate container.
    /// </summary>
    public QuerySubQueryContainer Source { get; }

    /// <summary>
    /// Alias assigned to the accumulator in the group stage.
    /// </summary>
    public string Alias { get; }

    /// <summary>
    /// Aggregate type applied.
    /// </summary>
    public Aggregate AggregateType { get; }

    public MongoAggregateBinding(QuerySubQueryContainer source, string alias, Aggregate aggregateType)
    {
        Source = source;
        Alias = alias ?? throw new ArgumentNullException(nameof(alias));
        AggregateType = aggregateType;
    }
}

