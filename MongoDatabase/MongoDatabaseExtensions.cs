using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;

namespace XpoNoSQL.MongoDatabase.Core;

public static class MongoDatabaseExtensions
{
    /// <summary>
    /// Produces a stable string key for cache lookups.
    /// </summary>
    public static string GetKey(this CriteriaOperator op)
    {
        switch (op)
        {
            case null:
                return "NULL";
            case QueryOperand qop:
                return $"QO:{qop.NodeAlias}:{MongoAliasRegistry.NormalizeColumnName(qop.ColumnName)}:{qop.ColumnType}";
            case OperandProperty prop:
                return $"OP:{MongoAliasRegistry.NormalizeColumnName(prop.PropertyName)}";
            case OperandValue or ConstantValue:
                return "VAL";
            case UnaryOperator uop:
                return $"UOP:{uop.OperatorType}:{uop.Operand.GetKey()}";
            case BinaryOperator bop:
                return $"BOP:{bop.OperatorType}:{bop.LeftOperand.GetKey()}:{bop.RightOperand.GetKey()}";
            case BetweenOperator between:
                return $"BET:{between.TestExpression.GetKey()}:{between.BeginExpression.GetKey()}:{between.EndExpression.GetKey()}";
            case InOperator inOp:
                {
                    var key = $"IN:{inOp.LeftOperand.GetKey()}";
                    foreach (var item in inOp.Operands)
                    {
                        key += $":{item.GetKey()}";
                    }
                    return key;
                }
            case GroupOperator group:
                {
                    var key = $"GRP:{group.OperatorType}";
                    foreach (var item in group.Operands)
                    {
                        key += $":{item.GetKey()}";
                    }
                    return key;
                }
            case FunctionOperator func:
                {
                    var opName = func.OperatorType.ToString();
                    if (func.OperatorType == FunctionOperatorType.Custom &&
                        func.Operands.Count > 0 &&
                        func.Operands[0] is OperandValue nameOperand &&
                        nameOperand.Value is string customName)
                    {
                        opName = $"Custom:{customName}";
                    }

                    var key = $"FN:{opName}";
                    foreach (var arg in func.Operands)
                    {
                        key += $":{arg.GetKey()}";
                    }
                    return key;
                }
            case QuerySubQueryContainer qsop:
                {
                    var aggKey = qsop.AggregateProperty?.GetKey() ?? "NOPROP";
                    var customName = qsop.AggregateType == Aggregate.Custom
                        ? (qsop.CustomAggregateName ?? string.Empty)
                        : string.Empty;
                    var customOperands = string.Empty;
                    if (qsop.AggregateType == Aggregate.Custom && qsop.CustomAggregateOperands is not null)
                    {
                        customOperands = $"ARGS:{qsop.CustomAggregateOperands.Count}";
                        foreach (var arg in qsop.CustomAggregateOperands)
                        {
                            customOperands += $":{arg.GetKey()}";
                        }
                    }
                    var nodeKey = qsop.Node is SelectStatement ss
                        ? $"SS:{ComputeSelectHash(ss)}"
                        : "NONODE";
                    return $"QSOP:{qsop.AggregateType}:{customName}:{customOperands}:{aggKey}:{nodeKey}";
                }
            default:
                return op.GetType().Name;
        }
    }

    public static int ComputeSelectHash(this SelectStatement select)
    {
        if (select is null)
        {
            return 0;
        }

        var hash = new HashCode();
        hash.Add(select.Table?.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        hash.Add(select.Alias ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        hash.Add(select.TopSelectedRecords);
        hash.Add(select.SkipSelectedRecords);

        HashCriteriaCollection(select.Operands, ref hash);
        HashCriteria(select.Condition, ref hash);
        HashCriteria(select.GroupCondition, ref hash);
        HashCriteriaCollection(select.GroupProperties, ref hash);

        if (select.SortProperties is not null)
        {
            foreach (var sort in select.SortProperties)
            {
                hash.Add(sort.Direction);
                HashCriteria(sort.Property, ref hash);
            }
        }

        if (select.SubNodes is not null)
        {
            foreach (var join in select.SubNodes)
            {
                HashJoin(join, ref hash);
            }
        }

        return hash.ToHashCode();
    }

    private static void HashJoin(JoinNode node, ref HashCode hash)
    {
        if (node is null)
        {
            return;
        }

        hash.Add(node.Table?.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        hash.Add(node.Alias ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        hash.Add(node.Type);
        HashCriteria(node.Condition, ref hash);

        if (node.SubNodes is not null)
        {
            foreach (var sub in node.SubNodes)
            {
                HashJoin(sub, ref hash);
            }
        }
    }

    private static void HashCriteriaCollection(CriteriaOperatorCollection collection, ref HashCode hash)
    {
        if (collection is null)
        {
            return;
        }

        foreach (var op in collection)
        {
            HashCriteria(op, ref hash);
        }
    }

    private static void HashCriteria(CriteriaOperator op, ref HashCode hash)
    {
        if (op is null)
        {
            hash.Add(nameof(NullValue));
            return;
        }

        switch (op)
        {
            case QueryOperand qo:
                hash.Add(nameof(QueryOperand));
                hash.Add(MongoAliasRegistry.NormalizeColumnName(qo.ColumnName));
                hash.Add(qo.ColumnType);
                hash.Add(qo.NodeAlias ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                break;
            case OperandProperty prop:
                hash.Add(nameof(OperandProperty));
                hash.Add(prop.PropertyName ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                break;
            case ConstantValue cv:
                hash.Add(nameof(ConstantValue));
                hash.Add(cv.Value?.GetType().FullName ?? "null", StringComparer.Ordinal);
                break;
            case OperandValue ov:
                hash.Add(nameof(OperandValue));
                hash.Add(ov.Value?.GetType().FullName ?? "null", StringComparer.Ordinal);
                break;
            case UnaryOperator unary:
                hash.Add(nameof(UnaryOperator));
                hash.Add(unary.OperatorType);
                HashCriteria(unary.Operand, ref hash);
                break;
            case BinaryOperator binary:
                hash.Add(nameof(BinaryOperator));
                hash.Add(binary.OperatorType);
                HashCriteria(binary.LeftOperand, ref hash);
                HashCriteria(binary.RightOperand, ref hash);
                break;
            case BetweenOperator between:
                hash.Add(nameof(BetweenOperator));
                HashCriteria(between.TestExpression, ref hash);
                HashCriteria(between.BeginExpression, ref hash);
                HashCriteria(between.EndExpression, ref hash);
                break;
            case InOperator inOp:
                hash.Add(nameof(InOperator));
                HashCriteria(inOp.LeftOperand, ref hash);
                foreach (var item in inOp.Operands)
                {
                    HashCriteria(item, ref hash);
                }
                break;
            case GroupOperator group:
                hash.Add(nameof(GroupOperator));
                hash.Add(group.OperatorType);
                foreach (var item in group.Operands)
                {
                    HashCriteria(item, ref hash);
                }
                break;
            case FunctionOperator func:
                hash.Add(nameof(FunctionOperator));
                hash.Add(func.OperatorType);
                if (func.OperatorType == FunctionOperatorType.Custom &&
                    func.Operands.Count > 0 &&
                    func.Operands[0] is OperandValue nameOperand &&
                    nameOperand.Value is string customName)
                {
                    hash.Add(customName, StringComparer.OrdinalIgnoreCase);
                }
                foreach (var operand in func.Operands)
                {
                    HashCriteria(operand, ref hash);
                }
                break;
            case QuerySubQueryContainer sub:
                hash.Add(nameof(QuerySubQueryContainer));
                hash.Add(sub.AggregateType);
                hash.Add(sub.CustomAggregateName ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                HashCriteria(sub.AggregateProperty, ref hash);
                if (sub.Node is SelectStatement nested)
                {
                    hash.Add(nested.ComputeSelectHash());
                }
                break;
            default:
                hash.Add(op.GetType().FullName ?? string.Empty, StringComparer.Ordinal);
                break;
        }
    }
}
