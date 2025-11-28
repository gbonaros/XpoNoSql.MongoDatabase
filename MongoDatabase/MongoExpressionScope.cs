// Part of the XpoNoSql.MongoDatabase provider.
// This file implements expression scope resolution for field paths and let variables as part of the XPO → MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using DevExpress.Xpo.DB;

using System;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Resolves QueryOperand and OperandProperty instances to Mongo field paths or let-variable references
/// within a particular translation scope.
/// </summary>
public sealed class MongoExpressionScope
{
    private readonly IDictionary<(string alias, string column), string> letVariables;

    /// <summary>
    /// Alias registry used to resolve collection/field paths.
    /// </summary>
    public MongoAliasRegistry Aliases { get; }

    /// <summary>
    /// Initializes a new scope with the provided alias registry.
    /// </summary>
    public MongoExpressionScope(MongoAliasRegistry aliases)
        : this(aliases, null)
    {
    }

    /// <summary>
    /// Initializes a new scope with alias registry and explicit let-variable bindings.
    /// </summary>
    public MongoExpressionScope(MongoAliasRegistry aliases, IDictionary<(string alias, string column), string> letVariables)
    {
        Aliases = aliases ?? throw new ArgumentNullException(nameof(aliases));
        this.letVariables = letVariables ?? new Dictionary<(string alias, string column), string>();
    }

    /// <summary>
    /// Creates a new scope with the same alias registry but different let-variable bindings.
    /// </summary>
    public MongoExpressionScope WithLet(IDictionary<(string alias, string column), string> variables)
    {
        return new MongoExpressionScope(Aliases, variables);
    }

    /// <summary>
    /// Resolves a QueryOperand to either a $$let reference or a field path in the current scope.
    /// </summary>
    public string Resolve(QueryOperand operand)
    {
        var normalizedColumn = MongoAliasRegistry.NormalizeColumnName(operand.ColumnName);
        var key = (operand.NodeAlias ?? Aliases.RootAlias, normalizedColumn);
        if (letVariables.TryGetValue(key, out var letName))
        {
            return $"$${letName}";
        }

        return Aliases.GetFieldPath(operand.NodeAlias, normalizedColumn);
    }

    /// <summary>
    /// Resolves an OperandProperty to a field path on the current document.
    /// </summary>
    public string ResolveProperty(string propertyName)
    {
        var normalized = MongoAliasRegistry.NormalizeColumnName(propertyName);
        return $"${normalized}";
    }
}

