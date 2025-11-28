// Part of the XpoNoSql.MongoDatabase provider.
// This file implements projection result metadata storage for translated selects as part of the XPO → MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using DevExpress.Data.Filtering;

using MongoDB.Bson;

using System;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Holds projection and alias metadata produced by <see cref="MongoProjectionBuilder"/>.
/// </summary>
public sealed class MongoProjectionResult
{
    /// <summary>
    /// Primary projection stage.
    /// </summary>
    public BsonDocument ProjectStage { get; }

    /// <summary>
    /// Final projection stage applied after sorting (if any).
    /// </summary>
    public BsonDocument FinalStage { get; }

    /// <summary>
    /// Mapping from expression keys to projection aliases.
    /// </summary>
    public IReadOnlyDictionary<string, string> ExpressionAliases { get; }

    /// <summary>
    /// Mapping from sort keys to projection aliases.
    /// </summary>
    public IReadOnlyDictionary<string, string> SortAliases { get; }

    /// <summary>
    /// User-facing projected fields.
    /// </summary>
    public ISet<string> UserFields { get; }

    public MongoProjectionResult(BsonDocument projectStage, BsonDocument finalStage, IDictionary<string, string> expressionAliases, IDictionary<string, string> sortAliases, ISet<string> userFields)
    {
        ProjectStage = projectStage;
        FinalStage = finalStage;
        ExpressionAliases = new Dictionary<string, string>(expressionAliases);
        SortAliases = new Dictionary<string, string>(sortAliases);
        UserFields = new HashSet<string>(userFields);
    }

    public bool TryGetSortAlias(CriteriaOperator criteria, out string alias)
    {
        return SortAliases.TryGetValue(MongoCriteriaTranslator.GetKey(criteria), out alias);
    }

    public bool TryGetAlias(CriteriaOperator criteria, out string alias)
    {
        return ExpressionAliases.TryGetValue(MongoCriteriaTranslator.GetKey(criteria), out alias);
    }
}

