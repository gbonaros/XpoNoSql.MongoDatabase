// Part of the XpoNoSql.MongoDatabase provider.
// This file implements grouping stage construction and aggregate discovery as part of the XPO -> MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;

using MongoDB.Bson;

using System;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Builds grouping metadata and the MongoDB <c>$group</c> stage, mirroring XPO grouping semantics.
/// The collected mapping allows downstream translators to reuse grouped keys and aggregate aliases consistently.
/// </summary>
public sealed class MongoGroupBuilder
{
    private readonly MongoTranslationContext context;
    private readonly MongoCriteriaTranslator translator;
    private readonly Dictionary<string, MongoGroupKeyBinding> groupKeys = new Dictionary<string, MongoGroupKeyBinding>();
    private readonly Dictionary<string, MongoAggregateBinding> aggregates = new Dictionary<string, MongoAggregateBinding>();

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoGroupBuilder"/> class.
    /// </summary>
    public MongoGroupBuilder(MongoTranslationContext context, MongoCriteriaTranslator translator)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.translator = translator ?? throw new ArgumentNullException(nameof(translator));
    }

    /// <summary>
    /// Scans the SelectStatement to collect grouping keys and aggregate containers referenced anywhere in the pipeline.
    /// </summary>
    public void Collect(SelectStatement statement)
    {
        if (statement is null)
        {
            return;
        }

        for (int i = 0; i < statement.GroupProperties.Count; i++)
        {
            RegisterGroupProperty(statement.GroupProperties[i], i);
        }

        foreach (var operand in statement.Operands)
        {
            VisitCriteria(operand);
        }

        VisitCriteria(statement.GroupCondition);

        foreach (var sort in statement.SortProperties)
        {
            VisitCriteria(sort.Property);
        }
    }

    /// <summary>
    /// Produces the <see cref="MongoGroupMapping"/> and corresponding <c>$group</c> stage if grouping is required.
    /// Returns an empty mapping when no grouping is necessary.
    /// </summary>
    public MongoGroupMapping Build()
    {
        bool requiresGrouping = groupKeys.Count > 0 || aggregates.Count > 0;
        if (!requiresGrouping)
        {
            return MongoGroupMapping.Empty;
        }

        var idDoc = new BsonDocument();
        foreach (var binding in groupKeys.Values)
        {
            var expr = TranslateGroupKey(binding.Source);
            idDoc[binding.Alias] = expr;
        }

        var groupBody = new BsonDocument
        {
            { "_id", idDoc.ElementCount > 0 ? idDoc : BsonNull.Value }
        };

        foreach (var aggregate in aggregates.Values)
        {
            var property = aggregate.Source.AggregateProperty;
            if (property is null)
            {
                if (aggregate.Source.AggregateType == Aggregate.Count || aggregate.Source.AggregateType == Aggregate.Exists)
                {
                    groupBody[aggregate.Alias] = new BsonDocument("$sum", 1);
                    continue;
                }

                throw new InvalidOperationException("Aggregate property is required for non-count aggregates.");
            }

            var expr = translator.TranslateExpression(property).Value;
            groupBody[aggregate.Alias] = BuildAccumulator(aggregate.AggregateType, expr);
        }

        var groupStage = new BsonDocument("$group", groupBody);
        return new MongoGroupMapping(true, groupStage, groupKeys, aggregates);
    }

    /// <summary>
    /// Registers a group key based on a group property.
    /// </summary>
    private void RegisterGroupProperty(CriteriaOperator criteria, int index)
    {
        var key = MongoGroupMapping.BuildKey(criteria);
        if (groupKeys.ContainsKey(key))
        {
            return;
        }

        var alias = DeriveGroupAlias(criteria, index);
        groupKeys.Add(key, new MongoGroupKeyBinding(criteria, alias));
    }

    /// <summary>
    /// Registers an aggregate container encountered in the query.
    /// </summary>
    private void RegisterAggregate(QuerySubQueryContainer container)
    {
        var key = MongoGroupMapping.BuildAggregateKey(container);
        if (aggregates.ContainsKey(key))
        {
            return;
        }

        var alias = $"Agg{aggregates.Count}";
        aggregates.Add(key, new MongoAggregateBinding(container, alias, container.AggregateType));
    }

    private static string DeriveGroupAlias(CriteriaOperator criteria, int index)
    {
        if (criteria is QueryOperand queryOperand && !string.IsNullOrEmpty(queryOperand.ColumnName))
        {
            return NormalizeAlias(queryOperand.ColumnName, index);
        }

        return $"K{index}";
    }

    private static string NormalizeAlias(string columnName, int index)
    {
        var normalized = MongoAliasRegistry.NormalizeColumnName(columnName);
        if (string.IsNullOrEmpty(normalized))
        {
            return $"K{index}";
        }

        normalized = normalized.Replace(".", "_");
        normalized = normalized.Replace("!", "_").Replace("\\", string.Empty);
        return normalized;
    }

    /// <summary>
    /// Builds the appropriate accumulator Bson expression for the aggregate type.
    /// </summary>
    private static BsonValue BuildAccumulator(Aggregate aggregateType, BsonValue expression)
    {
        switch (aggregateType)
        {
            case Aggregate.Sum:
                return new BsonDocument("$sum", expression);
            case Aggregate.Max:
                return new BsonDocument("$max", expression);
            case Aggregate.Min:
                return new BsonDocument("$min", expression);
            case Aggregate.Avg:
                return new BsonDocument("$avg", expression);
            case Aggregate.Count:
            case Aggregate.Exists:
                return new BsonDocument("$sum", 1);
            default:
                throw new NotSupportedException($"Aggregate '{aggregateType}' is not supported.");
        }
    }

    /// <summary>
    /// Recursively walks criteria to discover aggregates and group-by sources.
    /// </summary>
    private void VisitCriteria(CriteriaOperator criteria)
    {
        if (criteria is null)
        {
            return;
        }

        switch (criteria)
        {
            case QuerySubQueryContainer subQuery when subQuery.Node == null:
                RegisterAggregate(subQuery);
                break;
            case BetweenOperator between:
                VisitCriteria(between.BeginExpression);
                VisitCriteria(between.EndExpression);
                VisitCriteria(between.TestExpression);
                break;
            case BinaryOperator binary:
                VisitCriteria(binary.LeftOperand);
                VisitCriteria(binary.RightOperand);
                break;
            case GroupOperator group:
                foreach (var operand in group.Operands)
                {
                    VisitCriteria(operand);
                }

                break;
            case InOperator inOperator:
                VisitCriteria(inOperator.LeftOperand);
                foreach (var operand in inOperator.Operands)
                {
                    VisitCriteria(operand);
                }

                break;
            case UnaryOperator unary:
                VisitCriteria(unary.Operand);
                break;
            case FunctionOperator function:
                foreach (var operand in function.Operands)
                {
                    VisitCriteria(operand);
                }

                break;
        }
    }

    /// <summary>
    /// Translates a grouping key expression; uses truncation for ToInt buckets to preserve bucket semantics.
    /// </summary>
    private BsonValue TranslateGroupKey(CriteriaOperator criteria)
    {
        if (criteria is FunctionOperator func && func.OperatorType == FunctionOperatorType.ToInt && func.Operands.Count > 0)
        {
            var arg = translator.TranslateExpression(func.Operands[0]).Value;
            return new BsonDocument("$trunc", arg);
        }

        return translator.TranslateExpression(criteria).Value;
    }
}




