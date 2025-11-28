// Part of the XpoNoSql.MongoDatabase provider.
// This file implements alias-to-path resolution for SelectStatement roots and joins as part of the XPO → MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Helpers;
using DevExpress.Xpo.DB;

using System;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Tracks alias-to-path mappings for the root collection and joined collections.
/// Provides normalized field paths for criteria translation.
/// </summary>
public sealed class MongoAliasRegistry
{
    private readonly Dictionary<string, string> aliasToPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Root alias used for the base collection.
    /// </summary>
    public string RootAlias { get; }

    /// <summary>
    /// Name of the root MongoDB collection.
    /// </summary>
    public string RootCollection { get; }

    /// <summary>
    /// Initializes the registry with a root alias and root collection name.
    /// </summary>
    public MongoAliasRegistry(string rootAlias, string rootCollection)
    {
        RootAlias = string.IsNullOrEmpty(rootAlias) ? string.Empty : rootAlias;
        RootCollection = rootCollection ?? throw new ArgumentNullException(nameof(rootCollection));
        aliasToPath[RootAlias] = string.Empty;
    }

    /// <summary>
    /// Registers or overwrites an alias mapping to a dot-separated path.
    /// </summary>
    public void Register(string alias, string path)
    {
        alias ??= RootAlias;
        aliasToPath[alias] = path ?? string.Empty;
    }

    /// <summary>
    /// Checks whether the registry contains a mapping for the given alias.
    /// </summary>
    /// <param name="alias">Alias to test; defaults to root when null.</param>
    /// <returns>True when an alias mapping exists.</returns>
    public bool Contains(string alias)
    {
        alias ??= RootAlias;
        return aliasToPath.ContainsKey(alias);
    }

    /// <summary>
    /// Resolves a field path for the given alias and column name, returning a MongoDB field reference (with '$').
    /// </summary>
    public string GetFieldPath(string alias, string columnName)
    {
        alias ??= RootAlias;
        var normalizedColumn = NormalizeColumnName(columnName);
        if (!aliasToPath.TryGetValue(alias, out var path) || string.IsNullOrEmpty(path))
        {
            return $"${normalizedColumn}";
        }

        return $"${path}.{normalizedColumn}";
    }

    /// <summary>
    /// Enumerates all registered aliases.
    /// </summary>
    public IEnumerable<string> Aliases => aliasToPath.Keys;

    /// <summary>
    /// Collects aliases declared by the statement and its joins.
    /// </summary>
    public static HashSet<string> CollectAliases(BaseStatement statement)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (statement == null)
        {
            return aliases;
        }

        Collect(statement, aliases);
        return aliases;
    }

    /// <summary>
    /// Recursively collects aliases from a join tree.
    /// </summary>
    /// <param name="node">Root join node.</param>
    /// <param name="aliases">Alias accumulator.</param>
    private static void Collect(JoinNode node, ISet<string> aliases)
    {
        if (node == null)
        {
            return;
        }

        aliases.Add(node.Alias ?? string.Empty);

        foreach (var subNode in node.SubNodes)
        {
            Collect(subNode, aliases);
        }
    }

    /// <summary>
    /// Normalizes a column name by trimming type suffixes and XPO association markers.
    /// </summary>
    public static string NormalizeColumnName(string columnName)
    {
        if (string.IsNullOrEmpty(columnName))
        {
            return string.Empty;
        }

        var normalized = columnName;
        int commaIndex = normalized.IndexOf(',');
        if (commaIndex >= 0)
        {
            normalized = normalized.Substring(0, commaIndex);
        }

        normalized = normalized.Replace("!.", ".").Replace("!\\.", ".").Replace("\\", string.Empty);
        while (normalized.StartsWith(".", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(1);
        }

        while (normalized.EndsWith(".", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(0, normalized.Length - 1);
        }

        return normalized;
    }
}

