// Part of the XpoNoSql.MongoDatabase provider.
// This file implements internal group key binding metadata for grouped queries as part of the XPO → MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using DevExpress.Data.Filtering;

using System;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Represents a group key binding with the source criteria and assigned alias.
/// </summary>
public sealed class MongoGroupKeyBinding
{
    /// <summary>
    /// Source criteria operator for the grouping key.
    /// </summary>
    public CriteriaOperator Source { get; }

    /// <summary>
    /// Alias used in the grouping stage for this key.
    /// </summary>
    public string Alias { get; }

    public MongoGroupKeyBinding(CriteriaOperator source, string alias)
    {
        Source = source;
        Alias = alias ?? throw new ArgumentNullException(nameof(alias));
    }
}

