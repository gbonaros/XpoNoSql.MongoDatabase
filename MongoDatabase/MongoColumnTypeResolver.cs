// Part of the XpoNoSql.MongoDatabase provider.
// This file implements output column type inference for translated statements as part of the XPO → MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;

using System;
using System.Linq;
using System.Collections.Generic;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Traverses SelectStatement operands to infer <see cref="DBColumnType"/> information for output columns.
/// </summary>
public sealed class MongoColumnTypeResolver : IQueryCriteriaVisitor<DBColumnType>
{
    /// <summary>
    /// Resolved column types in operand order.
    /// </summary>
    public List<DBColumnType> Types { get; } = new List<DBColumnType>();

    /// <summary>
    /// Processes a statement to populate <see cref="Types"/> for each operand.
    /// </summary>
    public void Process(BaseStatement statement)
    {
        if (statement is null)
        {
            return;
        }

        Types.Clear();
        foreach (var op in statement.Operands)
        {
            Types.Add(Process(op));
        }
    }

    /// <summary>
    /// Dispatches a criteria node and returns its inferred column type.
    /// </summary>
    private DBColumnType Process(CriteriaOperator criteria)
    {
        if (criteria is null)
        {
            return DBColumnType.Unknown;
        }

        return criteria.Accept(this);
    }

    /// <inheritdoc />
    public DBColumnType Visit(QueryOperand theOperand)
    {
        var type = theOperand.ColumnType;
        return type;
    }

    public DBColumnType Visit(QuerySubQueryContainer theOperand)
    {
        if (theOperand is null)
        {
            return DBColumnType.Unknown;
        }

        switch (theOperand.AggregateType)
        {
            case Aggregate.Exists:
                return DBColumnType.Boolean;
            case Aggregate.Count:
                return DBColumnType.Int32;
            case Aggregate.Avg:
                {
                    var inner = Process(theOperand.AggregateProperty);
                    switch (inner)
                    {
                        case DBColumnType.Decimal:
                        case DBColumnType.Single:
                        case DBColumnType.Double:
                            return inner;
                        case DBColumnType.Int64:
                        case DBColumnType.UInt64:
                        case DBColumnType.Int32:
                        case DBColumnType.UInt32:
                        case DBColumnType.Int16:
                        case DBColumnType.UInt16:
                        case DBColumnType.Byte:
                        case DBColumnType.SByte:
                            return DBColumnType.Int32;
                        default:
                            return DBColumnType.Double;
                    }
                }
            case Aggregate.Custom:
                {
                    // Best effort: custom aggregate result type is unknown here.
                    foreach (var op in theOperand.CustomAggregateOperands)
                    {
                        Process(op);
                    }
                    return DBColumnType.Unknown;
                }
            default:
                return Process(theOperand.AggregateProperty);
        }
    }

    public DBColumnType Visit(BetweenOperator theOperator)
    {
        return DBColumnType.Unknown;
    }

    public DBColumnType Visit(BinaryOperator theOperator)
    {
        return DBColumnType.Unknown;
    }

    public DBColumnType Visit(UnaryOperator theOperator)
    {
        return DBColumnType.Unknown;
    }

    public DBColumnType Visit(InOperator theOperator)
    {
        return DBColumnType.Unknown;
    }

    public DBColumnType Visit(GroupOperator theOperator)
    {
        return DBColumnType.Unknown;
    }

    public DBColumnType Visit(OperandValue theOperand)
    {
        if (theOperand is null)
        {
            return DBColumnType.Unknown;
        }

        return MongoBsonValueConverter.DetermineColumnType(theOperand.Value);
    }

    public DBColumnType Visit(FunctionOperator theOperator)
    {
        switch (theOperator.OperatorType)
        {
            case FunctionOperatorType.ToInt:
                return DBColumnType.Int32;
            case FunctionOperatorType.ToLong:
                return DBColumnType.Int64;
            case FunctionOperatorType.ToDouble:
            case FunctionOperatorType.ToFloat:
                return DBColumnType.Double;
            case FunctionOperatorType.ToDecimal:
                return DBColumnType.Decimal;
            case FunctionOperatorType.Len:
                return DBColumnType.Int32;
            case FunctionOperatorType.GetYear:
            case FunctionOperatorType.GetMonth:
            case FunctionOperatorType.GetDay:
            case FunctionOperatorType.GetHour:
            case FunctionOperatorType.GetMinute:
            case FunctionOperatorType.GetSecond:
            case FunctionOperatorType.GetMilliSecond:
            case FunctionOperatorType.GetDayOfWeek:
            case FunctionOperatorType.GetDayOfYear:
                return DBColumnType.Int32;
            case FunctionOperatorType.Iif:
                {
                    // assume all branches same type as first branch
                    if (theOperator.Operands.Count >= 2)
                    {
                        return Process(theOperator.Operands[1]);
                    }
                    return DBColumnType.Unknown;
                }
            default:
                return DBColumnType.Unknown;
        }
    }
}
