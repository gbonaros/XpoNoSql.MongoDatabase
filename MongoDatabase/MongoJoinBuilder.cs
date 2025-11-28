// Part of the XpoNoSql.MongoDatabase provider.
// This file implements join translation via $lookup/$unwind as part of the XPO -> MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.

using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;

using MongoDB.Bson;

using System;
using System.Linq;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Translates XPO join nodes into MongoDB <c>$lookup</c>/<c>$unwind</c> stages, handling correlated predicates via let variables.
/// </summary>
public sealed class MongoJoinBuilder
{
    private readonly MongoTranslationContext context;
    private readonly MongoCriteriaTranslator translator;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoJoinBuilder"/> class.
    /// </summary>
    public MongoJoinBuilder(MongoTranslationContext context, MongoCriteriaTranslator translator)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.translator = translator ?? throw new ArgumentNullException(nameof(translator));
    }

    /// <summary>
    /// Emits lookup/unwind stages for all join nodes in the statement into the provided plan.
    /// </summary>
    public void BuildInto(MongoAggregationPlan plan)
    {
        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        context.Aliases.Register(context.Statement.Alias ?? string.Empty, string.Empty);
        foreach (var join in context.Statement.SubNodes)
        {
            AppendJoinNode(join, plan);
        }
    }

    /// <summary>
    /// Recursively appends a join node and its children to the aggregation plan.
    /// </summary>
    private void AppendJoinNode(JoinNode node, MongoAggregationPlan plan)
    {
        if (node == null)
        {
            return;
        }

        var joinAlias = string.IsNullOrEmpty(node.Alias) ? node.Table?.Name ?? $"j{plan.Pipeline.Count}" : node.Alias;
        var letVariables = CollectLetVariables(node, joinAlias);
        var letDictionary = letVariables.ToDictionary(k => (k.Key.alias, k.Key.column), v => v.Value);
        var letDoc = new BsonDocument();
        foreach (var kvp in letVariables)
        {
            letDoc[kvp.Value] = BsonValue.Create(kvp.Key.expression);
        }

        var registry = new MongoAliasRegistry(joinAlias, node.Table.Name);
        var scope = new MongoExpressionScope(registry, letDictionary);
        var joinTranslator = new MongoCriteriaTranslator(context, scope);

        var pipeline = new BsonArray();
        var match = joinTranslator.TranslateMatch(node.Condition);
        if (match != null)
        {
            pipeline.Add(new BsonDocument("$match", match));
        }

        var lookup = new BsonDocument
        {
            { "from", node.Table.Name },
            { "as", joinAlias },
            { "pipeline", pipeline }
        };

        if (letDoc.ElementCount > 0)
        {
            lookup.Add("let", letDoc);
        }

        plan.AddStage(new BsonDocument("$lookup", lookup));
        plan.AddStage(new BsonDocument("$unwind", new BsonDocument
        {
            { "path", $"${joinAlias}" },
            { "preserveNullAndEmptyArrays", node.Type != JoinType.Inner }
        }));

        context.Aliases.Register(joinAlias, joinAlias);
        foreach (var sub in node.SubNodes)
        {
            AppendJoinNode(sub, plan);
        }
    }

    /// <summary>
    /// Collects outer references from the join condition to populate <c>let</c> variables for correlation.
    /// </summary>
    private List<KeyValuePair<(string alias, string column, string expression), string>> CollectLetVariables(JoinNode node, string joinAlias)
    {
        var result = new List<KeyValuePair<(string alias, string column, string expression), string>>();
        void Visit(CriteriaOperator op)
        {
            if (op == null)
            {
                return;
            }

            switch (op)
            {
                case QueryOperand qo:
                    {
                        var alias = qo.NodeAlias ?? context.Aliases.RootAlias;
                        if (string.Equals(alias, joinAlias, StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        if (!context.Aliases.Contains(alias))
                        {
                            return;
                        }

                        var normalizedColumn = MongoAliasRegistry.NormalizeColumnName(qo.ColumnName);
                        if (result.Any(r => string.Equals(r.Key.alias, alias, StringComparison.OrdinalIgnoreCase) && string.Equals(r.Key.column, normalizedColumn, StringComparison.OrdinalIgnoreCase)))
                        {
                            return;
                        }

                        var expression = context.Scope.Resolve(qo);
                        result.Add(new KeyValuePair<(string alias, string column, string expression), string>((alias, normalizedColumn, expression), $"outer_{result.Count}"));
                        break;
                    }
                case BinaryOperator binary:
                    Visit(binary.LeftOperand);
                    Visit(binary.RightOperand);
                    break;
                case BetweenOperator between:
                    Visit(between.BeginExpression);
                    Visit(between.EndExpression);
                    Visit(between.TestExpression);
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
            }
        }

        Visit(node.Condition);
        return result;
    }
}

