// Part of the XpoNoSql.MongoDatabase provider.
// This file implements grouping metadata mapping for grouped keys and aggregates as part of the XPO → MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;

using MongoDB.Bson;

using System;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Captures grouping state: the generated <c>$group</c> stage plus alias mappings for grouped keys and aggregates.
/// </summary>
public sealed class MongoGroupMapping
{
    public static readonly MongoGroupMapping Empty = new MongoGroupMapping(hasGrouping: false, groupStage: null, new Dictionary<string, MongoGroupKeyBinding>(), new Dictionary<string, MongoAggregateBinding>());

    /// <summary>
    /// Indicates whether grouping was applied.
    /// </summary>
    public bool HasGrouping { get; }

    /// <summary>
    /// The generated <c>$group</c> stage document (or null when grouping is not used).
    /// </summary>
    public BsonDocument GroupStage { get; }

    /// <summary>
    /// Group key bindings keyed by criteria string.
    /// </summary>
    public IReadOnlyDictionary<string, MongoGroupKeyBinding> GroupKeys { get; }

    /// <summary>
    /// Aggregate bindings keyed by aggregate signature.
    /// </summary>
    public IReadOnlyDictionary<string, MongoAggregateBinding> Aggregates { get; }

    /// <summary>
    /// Creates a new mapping instance with provided grouping and aggregate bindings.
    /// </summary>
    internal MongoGroupMapping(bool hasGrouping, BsonDocument groupStage, IDictionary<string, MongoGroupKeyBinding> groupKeys, IDictionary<string, MongoAggregateBinding> aggregates)
    {
        HasGrouping = hasGrouping;
        GroupStage = groupStage;
        GroupKeys = new Dictionary<string, MongoGroupKeyBinding>(groupKeys ?? new Dictionary<string, MongoGroupKeyBinding>());
        Aggregates = new Dictionary<string, MongoAggregateBinding>(aggregates ?? new Dictionary<string, MongoAggregateBinding>());
    }

    internal static string BuildKey(CriteriaOperator op)
    {
        return op?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Builds a stable key for aggregate containers (including custom aggregates).
    /// </summary>
    internal static string BuildAggregateKey(QuerySubQueryContainer container)
    {
        if (container == null)
        {
            return string.Empty;
        }

        if (container.AggregateType != Aggregate.Custom)
        {
            return $"{container.AggregateType}:{container.AggregateProperty?.ToString() ?? string.Empty}:{container.Node?.ToString() ?? string.Empty}";
        }

        return $"{container.CustomAggregateName}:{container.CustomAggregateOperands?.ToString() ?? string.Empty}:{container.Node?.ToString() ?? string.Empty}";
    }

    /// <summary>
    /// Attempts to resolve a criteria operator to its grouped key expression.
    /// </summary>
    public bool TryResolveGroupValue(CriteriaOperator op, out MongoExpression expression)
    {
        expression = default;
        if (op == null)
        {
            return false;
        }

        if (GroupKeys.TryGetValue(BuildKey(op), out var binding))
        {
            expression = MongoExpression.Field($"$_id.{binding.Alias}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to resolve an aggregate container to its accumulator expression.
    /// </summary>
    public bool TryResolveAggregate(QuerySubQueryContainer container, out MongoExpression expression)
    {
        expression = default;
        if (container == null)
        {
            return false;
        }

        if (Aggregates.TryGetValue(BuildAggregateKey(container), out var binding))
        {
            var field = MongoExpression.Field($"${binding.Alias}");
            if (binding.AggregateType == Aggregate.Exists)
            {
                expression = MongoExpression.Raw(new BsonDocument("$gt", new BsonArray
                {
                    field.Value,
                    0
                }));
            }
            else
            {
                expression = field;
            }

            return true;
        }

        return false;
    }
}

