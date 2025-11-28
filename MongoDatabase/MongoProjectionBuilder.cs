// Part of the XpoNoSql.MongoDatabase provider.
// This file implements projection construction and sort aliasing as part of the XPO → MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;

using MongoDB.Bson;

using System;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Builds projection documents and sort aliases for translated select statements.
/// </summary>
public sealed class MongoProjectionBuilder
{
    private readonly MongoTranslationContext context;
    private readonly MongoGroupMapping groupMapping;
    private readonly MongoCriteriaTranslator translator;

    /// <summary>
    /// Creates a new projection builder for the given translation context and grouping mapping.
    /// </summary>
    public MongoProjectionBuilder(MongoTranslationContext context, MongoGroupMapping groupMapping, MongoCriteriaTranslator translator)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.groupMapping = groupMapping ?? MongoGroupMapping.Empty;
        this.translator = translator ?? throw new ArgumentNullException(nameof(translator));
    }

    /// <summary>
    /// Builds a projection stage, optional final projection, and alias mappings for expressions and sorts.
    /// </summary>
    public MongoProjectionResult Build(IList<CriteriaOperator> operands, QuerySortingCollection sortProperties, IList<string> propertyAliases)
    {
        var projectDoc = new BsonDocument
        {
            { "_id", 0 }
        };
        var expressionAliases = new Dictionary<string, string>();
        var userFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (operands != null)
        {
            for (int i = 0; i < operands.Count; i++)
            {
                var operand = operands[i];
                var alias = ResolveAliasForOperand(i, operand, propertyAliases, userFields);
                var expr = translator.TranslateExpression(operand);
                projectDoc[alias] = expr.Value;
                expressionAliases[MongoCriteriaTranslator.GetKey(operand)] = alias;
                expressionAliases[alias] = alias; // allow OperandProperty sorts by alias name
                userFields.Add(alias);
            }
        }

        var sortAliases = new Dictionary<string, string>();
        int sortCounter = 0;
        if (sortProperties != null)
        {
            foreach (var sort in sortProperties)
            {
                var key = MongoCriteriaTranslator.GetKey(sort.Property);
                var needsCaseInsensitive = NeedsCaseInsensitiveSort(sort.Property);
                var alias = $"_sort{sortCounter++}";
                var expr = translator.TranslateExpression(sort.Property);
                if (needsCaseInsensitive)
                {
                    projectDoc[alias] = new BsonDocument("$toLower", expr.Value);
                }
                else
                {
                    projectDoc[alias] = expr.Value;
                }

                sortAliases[key] = alias;
                sortAliases[alias] = alias;
            }
        }

        BsonDocument finalProject = null;
        if (sortCounter > 0)
        {
            finalProject = new BsonDocument
            {
                { "_id", 0 }
            };

            foreach (var field in userFields)
            {
                finalProject[field] = 1;
            }
        }

        return new MongoProjectionResult(projectDoc, finalProject, expressionAliases, sortAliases, userFields);
    }

    internal static string ResolveAliasForOperand(int index, CriteriaOperator operand, IList<string> propertyAliases, ISet<string> usedAliases = null)
    {
        var aliasFromList = propertyAliases != null && index < propertyAliases.Count ? propertyAliases[index] : null;
        if (operand is QueryOperand queryOperand && !string.IsNullOrEmpty(queryOperand.ColumnName))
        {
            if (!string.IsNullOrEmpty(aliasFromList))
            {
                return SanitizeAlias(aliasFromList);
            }

            var normalized = MongoAliasRegistry.NormalizeColumnName(queryOperand.ColumnName);
            var candidate = string.IsNullOrEmpty(queryOperand.NodeAlias)
                ? normalized
                : $"{queryOperand.NodeAlias}_{normalized}";

            if (string.IsNullOrEmpty(candidate))
            {
                candidate = $"PrP{index}";
            }

            if (usedAliases != null && usedAliases.Contains(candidate))
            {
                candidate = $"{candidate}_{index}";
            }

            return SanitizeAlias(candidate);
        }

        if (!string.IsNullOrEmpty(aliasFromList))
        {
            return SanitizeAlias(aliasFromList);
        }

        return SanitizeAlias($"PrP{index}");
    }

    private static string SanitizeAlias(string alias)
    {
        if (string.IsNullOrEmpty(alias))
        {
            return alias;
        }

        return alias.Replace(".", "_").Replace("$", "_");
    }

    private static bool NeedsCaseInsensitiveSort(CriteriaOperator op)
    {
        if (op is QueryOperand qo)
        {
            return qo.ColumnType == DBColumnType.String || qo.ColumnType == DBColumnType.Unknown;
        }

        if (op is OperandProperty)
        {
            return true;
        }

        return false;
    }
}
