// Part of the XpoNoSql.MongoDatabase provider.
// This file implements result materialization from MongoDB documents into XPO SelectedData as part of the XPO -> MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;

using MongoDB.Bson;

using System;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Materializes aggregation results into <see cref="SelectStatementResult"/> instances expected by XPO.
/// </summary>
public sealed class MongoResultMaterializer
{
    private sealed class ColumnDescriptor
    {
        public string Alias { get; set; }
        public DBColumnType ColumnType { get; set; }
    }

    /// <summary>
    /// Materializes BSON documents into a <see cref="SelectStatementResult"/> honoring operand aliases and column types.
    /// </summary>
    public SelectStatementResult Materialize(SelectStatement select, IReadOnlyList<BsonDocument> documents, MongoTranslationContext context)
    {
        if (select == null)
        {
            throw new ArgumentNullException(nameof(select));
        }

        if (documents == null)
        {
            throw new ArgumentNullException(nameof(documents));
        }

        var dbtypes = new MongoColumnTypeResolver();
        dbtypes.Process(select);

        var descriptors = BuildColumnDescriptors(select, context?.PropertyAliases, dbtypes.Types);
        var rows = new List<SelectStatementResultRow>(documents.Count);
        foreach (var doc in documents)
        {
            var values = new object[descriptors.Count];
            for (int i = 0; i < descriptors.Count; i++)
            {
                var descriptor = descriptors[i];
                var bsonValue = TryGetValue(doc, descriptor.Alias);
                values[i] = MongoBsonValueConverter.Convert(bsonValue, descriptor.ColumnType);
            }

            rows.Add(new SelectStatementResultRow(values));
        }

        return new SelectStatementResult(rows.ToArray());
    }

    private static List<ColumnDescriptor> BuildColumnDescriptors(SelectStatement select, IList<string> propertyAliases, IList<DBColumnType> resolvedTypes)
    {
        var list = new List<ColumnDescriptor>();
        var operands = select.Operands ?? new CriteriaOperatorCollection();
        var usedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < operands.Count; i++)
        {
            var operand = operands[i];
            var alias = MongoProjectionBuilder.ResolveAliasForOperand(i, operand, propertyAliases, usedAliases);
            var columnType = resolvedTypes != null && i < resolvedTypes.Count
                ? resolvedTypes[i]
                : DetermineColumnType(operand);
            list.Add(new ColumnDescriptor
            {
                Alias = alias,
                ColumnType = columnType
            });
            usedAliases.Add(alias);
        }

        return list;
    }

    private static DBColumnType DetermineColumnType(CriteriaOperator operand)
    {
        switch (operand)
        {
            case QueryOperand queryOperand:
                return queryOperand.ColumnType;
            case OperandValue operandValue:
                return MongoBsonValueConverter.DetermineColumnType(operandValue.Value);
            case QuerySubQueryContainer:
            default:
                return DBColumnType.Unknown;
        }
    }

    private static BsonValue TryGetValue(BsonDocument document, string alias)
    {
        if (document == null || string.IsNullOrEmpty(alias))
        {
            return BsonNull.Value;
        }

        if (document.TryGetValue(alias, out var direct))
        {
            return direct;
        }

        // If an alias accidentally contains dots, traverse the path.
        var parts = alias.Split('.');
        BsonValue current = document;
        foreach (var part in parts)
        {
            if (current is BsonDocument currentDoc && currentDoc.TryGetValue(part, out var next))
            {
                current = next;
            }
            else
            {
                return BsonNull.Value;
            }
        }

        return current;
    }
}
