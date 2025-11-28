using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;

using MongoDB.Bson;

using System;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Responsible for emitting the final <c>$sort</c> stage for translated SelectStatements.
/// Sort resolution relies on projection aliases produced earlier in the pipeline and, when grouping is present,
/// on group/aggregate mappings to ensure correct field names are used in the sort document.
/// </summary>
public static class MongoSortBuilder
{
    /// <summary>
    /// Builds a <c>$sort</c> BsonDocument using sort properties and previously built projection/group mapping.
    /// Returns <c>null</c> when no sorting is requested.
    /// </summary>
    public static BsonDocument Build(QuerySortingCollection sortProperties, MongoProjectionResult projection, MongoGroupMapping groupMapping = null)
    {
        if (sortProperties == null || sortProperties.Count == 0)
        {
            return null;
        }

        if (projection == null)
        {
            throw new ArgumentNullException(nameof(projection));
        }

        var sortDoc = new BsonDocument();
        int autoCounter = 0;

        // Iterate through requested sorts in order, mapping each to a projection alias or group/aggregate expression.
        foreach (var sort in sortProperties)
        {
            string alias = null;
            var projectedAlias = $"_sort{autoCounter}";

            if (alias == null && projection.TryGetAlias(sort.Property, out var exprAlias))
            {
                alias = exprAlias;
            }

            if (sort.Property is OperandProperty op && projection.ProjectStage.Contains(op.PropertyName))
            {
                alias = op.PropertyName;
            }
            else if (projection.ProjectStage.Contains(projectedAlias))
            {
                alias = projectedAlias;
            }

            if (alias == null && !projection.TryGetSortAlias(sort.Property, out alias))
            {
                if (groupMapping != null)
                {
                    if (groupMapping.TryResolveGroupValue(sort.Property, out var groupExpr))
                    {
                        alias = groupExpr.GetFieldName();
                    }
                    else if (sort.Property is QuerySubQueryContainer sub && groupMapping.TryResolveAggregate(sub, out var aggExpr))
                    {
                        alias = aggExpr.IsField ? aggExpr.GetFieldName() : null;
                    }
                }

                if (alias == null)
                {
                    autoCounter++;
                    continue;
                }
            }

            // If the alias refers to _id.<field> but the field was projected to the top level,
            // trim the prefix so the sort targets the projected field.
            if (alias.StartsWith("_id.", StringComparison.Ordinal) && projection.UserFields != null)
            {
                var trimmed = alias.Substring("_id.".Length);
                if (projection.UserFields.Contains(trimmed))
                {
                    alias = trimmed;
                }
            }

            sortDoc[alias] = sort.Direction == SortingDirection.Descending ? -1 : 1;
            autoCounter++;
        }

        if (sortDoc.ElementCount == 0)
        {
            return null;
        }

        return new BsonDocument("$sort", sortDoc);
    }
}

