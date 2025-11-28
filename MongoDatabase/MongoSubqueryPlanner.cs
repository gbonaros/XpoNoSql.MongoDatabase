// Part of the XpoNoSql.MongoDatabase provider.
// This file implements subquery planning and correlated lookup staging as part of the XPO → MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;

using MongoDB.Bson;

using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace XpoNoSQL.MongoDatabase.Core;

public sealed class MongoSubqueryPlanner
{
    private readonly MongoTranslationContext context;

    private readonly Dictionary<QuerySubQueryContainer, string> aliases;

    private readonly List<BsonDocument> stages;

    public MongoSubqueryPlanner(MongoTranslationContext context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        aliases = new Dictionary<QuerySubQueryContainer, string>(ReferenceEqualityComparer<QuerySubQueryContainer>.Instance);
        stages = new List<BsonDocument>();
    }

    public IReadOnlyList<BsonDocument> Stages => stages;

    public void PlanForStatement(SelectStatement statement)
    {
        if (statement == null)
        {
            return;
        }

        foreach (var operand in statement.Operands)
        {
            VisitCriteria(operand);
        }

        VisitCriteria(statement.Condition);
        VisitCriteria(statement.GroupCondition);
        foreach (var group in statement.GroupProperties)
        {
            VisitCriteria(group);
        }

        foreach (var sort in statement.SortProperties)
        {
            VisitCriteria(sort.Property);
        }

        foreach (var join in statement.SubNodes)
        {
            PlanJoin(join);
        }
    }

    private void PlanJoin(JoinNode node)
    {
        if (node == null)
        {
            return;
        }

        VisitCriteria(node.Condition);
        foreach (var subNode in node.SubNodes)
        {
            PlanJoin(subNode);
        }
    }

    private void VisitCriteria(CriteriaOperator criteria)
    {
        if (ReferenceEquals(criteria, null))
        {
            return;
        }

        switch (criteria)
        {
            case QuerySubQueryContainer subQuery when subQuery.Node != null:
                EnsureLookup(subQuery, context.Scope);
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
                foreach (var op in group.Operands)
                {
                    VisitCriteria(op);
                }

                break;
            case InOperator inOp:
                VisitCriteria(inOp.LeftOperand);
                foreach (var op in inOp.Operands)
                {
                    VisitCriteria(op);
                }

                break;
            case UnaryOperator unary:
                VisitCriteria(unary.Operand);
                break;
            case FunctionOperator function:
                foreach (var op in function.Operands)
                {
                    VisitCriteria(op);
                }

                break;
        }
    }

    public string EnsureLookup(QuerySubQueryContainer container, MongoExpressionScope callerScope)
    {
        if (aliases.TryGetValue(container, out var existing))
        {
            return existing;
        }

        var alias = $"SubAgg{aliases.Count}";
        aliases.Add(container, alias);

        var statement = container.Node as SelectStatement;
        if (statement == null)
        {
            throw new InvalidOperationException("Only SelectStatement nodes are supported inside QuerySubQueryContainer.");
        }

        var innerAliases = BuildInnerAliases(statement);
        var letMapping = CollectLetVariables(statement, callerScope, innerAliases);
        var letDoc = new BsonDocument();
        foreach (var kvp in letMapping)
        {
            letDoc[kvp.Value] = BsonValue.Create(kvp.Key.expression);
        }

        var subAliases = new MongoAliasRegistry(statement.Alias ?? string.Empty, statement.Table.Name);
        var subScope = new MongoExpressionScope(subAliases, letMapping.ToDictionary(k => (k.Key.alias, k.Key.column), v => v.Value));
        var subContext = context.CreateChild(statement, subAliases, subScope);
        var subTranslator = new MongoCriteriaTranslator(subContext, subScope);
        var joinBuilder = new MongoJoinBuilder(subContext, subTranslator);
        joinBuilder.BuildInto(subContext.Plan);
        subContext.SubqueryPlanner.PlanForStatement(statement);

        subContext.Plan.AddStages(subContext.SubqueryPlanner.Stages);

        var where = subTranslator.TranslateMatch(statement.Condition);
        if (where != null)
        {
            subContext.Plan.AddStage("$match", where);
        }

        var groupBuilder = new MongoGroupBuilder(subContext, subTranslator);
        groupBuilder.Collect(statement);
        var mapping = groupBuilder.Build();
        subContext.GroupMapping = mapping;
        if (mapping.HasGrouping)
        {
            subContext.Plan.AddStage(mapping.GroupStage);
        }

        var groupedTranslator = mapping.HasGrouping ? subTranslator.WithGroup(mapping) : subTranslator;
        var having = groupedTranslator.TranslateMatch(statement.GroupCondition);
        if (having != null)
        {
            subContext.Plan.AddStage("$match", having);
        }

        var aggregateProperty = container.AggregateProperty;
        if (ReferenceEquals(aggregateProperty, null) && statement.Operands.Count > 0)
        {
            aggregateProperty = statement.Operands[0];
        }

        var accumulatorExpression = ReferenceEquals(aggregateProperty, null) ? MongoExpression.Constant(1).Value : groupedTranslator.TranslateExpression(aggregateProperty).Value;
        var groupBody = new BsonDocument
        {
            { "_id", BsonNull.Value },
            { "agg", BuildAccumulator(container.AggregateType, accumulatorExpression) }
        };
        subContext.Plan.AddStage("$group", groupBody);

        var projectedValue = container.AggregateType == Aggregate.Exists ? new BsonDocument("$gt", new BsonArray { "$agg", 0 }) : (BsonValue)"$agg";
        subContext.Plan.AddStage("$project", new BsonDocument("value", projectedValue));

        var lookupBody = new BsonDocument
        {
            { "from", statement.Table.Name },
            { "as", alias }
        };

        if (letDoc.ElementCount > 0)
        {
            lookupBody.Add("let", letDoc);
        }

        var pipelineArray = new BsonArray(subContext.Plan.Pipeline);
        if (pipelineArray.Count > 0)
        {
            lookupBody.Add("pipeline", pipelineArray);
        }

        stages.Add(new BsonDocument("$lookup", lookupBody));
        stages.Add(new BsonDocument("$unwind", new BsonDocument
        {
            { "path", $"${alias}" },
            { "preserveNullAndEmptyArrays", true }
        }));

        var defaultValue = GetDefaultValue(container.AggregateType);
        stages.Add(new BsonDocument("$addFields", new BsonDocument(alias, new BsonDocument("$ifNull", new BsonArray
        {
            $"${alias}.value",
            defaultValue
        }))));

        return alias;
    }

    private static HashSet<string> BuildInnerAliases(SelectStatement statement)
    {
        var inner = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Collect(JoinNode node)
        {
            if (node == null)
            {
                return;
            }

            inner.Add(node.Alias ?? string.Empty);
            foreach (var sub in node.SubNodes)
            {
                Collect(sub);
            }
        }

        Collect(statement);
        return inner;
    }

    private static BsonValue GetDefaultValue(Aggregate aggregate)
    {
        switch (aggregate)
        {
            case Aggregate.Count:
                return 0;
            case Aggregate.Sum:
                return BsonNull.Value;
            case Aggregate.Exists:
                return false;
            default:
                return BsonNull.Value;
        }
    }

    private static BsonValue BuildAccumulator(Aggregate aggregateType, BsonValue expression)
    {
        switch (aggregateType)
        {
            case Aggregate.Count:
            case Aggregate.Exists:
                return new BsonDocument("$sum", 1);
            case Aggregate.Sum:
                return new BsonDocument("$sum", expression);
            case Aggregate.Max:
                return new BsonDocument("$max", expression);
            case Aggregate.Min:
                return new BsonDocument("$min", expression);
            case Aggregate.Avg:
                return new BsonDocument("$avg", expression);
            default:
                throw new NotSupportedException($"Aggregate '{aggregateType}' is not supported in subqueries.");
        }
    }

    private List<KeyValuePair<(string alias, string column, string expression), string>> CollectLetVariables(SelectStatement statement, MongoExpressionScope callerScope, HashSet<string> innerAliases)
    {
        var result = new List<KeyValuePair<(string alias, string column, string expression), string>>();
        foreach (var operand in CollectOperands(statement))
        {
            var alias = operand.NodeAlias ?? string.Empty;
            if (innerAliases.Contains(alias))
            {
                continue;
            }

            var normalizedColumn = MongoAliasRegistry.NormalizeColumnName(operand.ColumnName);
            if (result.Any(p => string.Equals(p.Key.alias, alias, StringComparison.OrdinalIgnoreCase) && string.Equals(p.Key.column, normalizedColumn, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var expression = callerScope.Resolve(operand);
            var letName = $"outer_{result.Count}";
            result.Add(new KeyValuePair<(string alias, string column, string expression), string>((alias, normalizedColumn, expression), letName));
        }

        return result;
    }

    private IEnumerable<QueryOperand> CollectOperands(SelectStatement statement)
    {
        var collector = new List<QueryOperand>();
        void Visit(CriteriaOperator op)
        {
            if (ReferenceEquals(op, null))
            {
                return;
            }

            switch (op)
            {
                case QueryOperand qo:
                    collector.Add(qo);
                    break;
                case BetweenOperator between:
                    Visit(between.BeginExpression);
                    Visit(between.EndExpression);
                    Visit(between.TestExpression);
                    break;
                case BinaryOperator binary:
                    Visit(binary.LeftOperand);
                    Visit(binary.RightOperand);
                    break;
                case GroupOperator group:
                    foreach (var operand in group.Operands)
                    {
                        Visit(operand);
                    }

                    break;
                case InOperator inOperator:
                    Visit(inOperator.LeftOperand);
                    foreach (var operand in inOperator.Operands)
                    {
                        Visit(operand);
                    }

                    break;
                case UnaryOperator unary:
                    Visit(unary.Operand);
                    break;
                case FunctionOperator function:
                    foreach (var operand in function.Operands)
                    {
                        Visit(operand);
                    }

                    break;
                case QuerySubQueryContainer container:
                    if (container.Node != null)
                    {
                        foreach (var nested in CollectOperands((SelectStatement)container.Node))
                        {
                            collector.Add(nested);
                        }
                    }

                    break;
            }
        }

        foreach (var operand in statement.Operands)
        {
            Visit(operand);
        }

        Visit(statement.Condition);
        Visit(statement.GroupCondition);
        foreach (var groupProp in statement.GroupProperties)
        {
            Visit(groupProp);
        }

        foreach (var sort in statement.SortProperties)
        {
            Visit(sort.Property);
        }

        foreach (var join in statement.SubNodes)
        {
            Visit(join.Condition);
        }

        return collector;
    }
}


