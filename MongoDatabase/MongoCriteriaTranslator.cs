// Part of the XpoNoSql.MongoDatabase provider.
// This file implements criteria translation from XPO filter operators to MongoDB expressions and match documents as part of the XPO ? MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Helpers;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;

using MongoDB.Bson;

using System;
using System.Linq;
using System.Text;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Translates XPO criteria operators into MongoDB expressions and match documents.
/// Handles grouping mappings, subquery references, function operators, and $expr decisions.
/// </summary>
public sealed class MongoCriteriaTranslator : IQueryCriteriaVisitor<MongoExpression>, ICriteriaVisitor<MongoExpression>
{
    private readonly MongoTranslationContext context;

    private readonly MongoExpressionScope scope;

    private readonly MongoGroupMapping groupMapping;

    /// <summary>
    /// Initializes a translator for criteria, optionally with grouping resolution.
    /// </summary>
    /// <param name="context">Translation context containing planning state.</param>
    /// <param name="scope">Expression scope for resolving operands.</param>
    /// <param name="groupMapping">Optional grouping mapping used for grouped queries.</param>
    public MongoCriteriaTranslator(MongoTranslationContext context, MongoExpressionScope scope, MongoGroupMapping groupMapping = null)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
        this.groupMapping = groupMapping;
    }

    /// <summary>
    /// Creates a new translator bound to the provided grouping mapping.
    /// </summary>
    public MongoCriteriaTranslator WithGroup(MongoGroupMapping mapping)
    {
        return new MongoCriteriaTranslator(context, scope, mapping);
    }

    /// <summary>
    /// Produces a stable string key for cache lookups.
    /// </summary>
    public static string GetKey(CriteriaOperator op)
    {
        return op?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Translates a criteria operator into a MongoDB <c>$match</c> document, falling back to <c>$expr</c> when needed.
    /// </summary>
    public BsonDocument TranslateMatch(CriteriaOperator criteria)
    {
        if (ReferenceEquals(criteria, null))
        {
            return null;
        }

        var filter = TranslateMatchInternal(criteria);
        if (filter != null && filter.ElementCount > 0)
        {
            return filter;
        }

        var expr = TranslateExpression(criteria).Value;
        return new BsonDocument("$expr", expr);
    }

    private BsonDocument TranslateMatchInternal(CriteriaOperator criteria)
    {
        switch (criteria)
        {
            case BinaryOperator binary:
                {
                    var left = TranslateExpression(binary.LeftOperand);
                    var right = TranslateExpression(binary.RightOperand);
                    if (TryBuildComparisonFilter(binary.OperatorType, left, right, out var direct))
                    {
                        return direct;
                    }

                    return new BsonDocument("$expr", TranslateBinary(binary, left, right).Value);
                }
            case GroupOperator group:
                {
                    var filters = new BsonArray();
                    var exprArray = new BsonArray();
                    bool requiresExpr = false;
                    foreach (var operand in group.Operands)
                    {
                        var nested = TranslateMatchInternal(operand);
                        if (nested != null && nested.ElementCount > 0)
                        {
                            filters.Add(nested);
                            if (nested.Contains("$expr"))
                            {
                                requiresExpr = true;
                                exprArray.Add(nested["$expr"]);
                            }
                            else
                            {
                                requiresExpr = requiresExpr || ContainsLetReference(nested);
                                exprArray.Add(MatchDocumentToExpr(nested));
                            }
                        }
                        else
                        {
                            var expr = TranslateExpression(operand).Value;
                            var exprDoc = new BsonDocument("$expr", expr);
                            filters.Add(exprDoc);
                            requiresExpr = true;
                            exprArray.Add(expr);
                        }
                    }

                    if (filters.Count == 0)
                    {
                        return null;
                    }

                    var opName = group.OperatorType == GroupOperatorType.And ? "$and" : "$or";
                    if (requiresExpr)
                    {
                        return new BsonDocument("$expr", new BsonDocument(opName, exprArray));
                    }

                    return new BsonDocument(opName, filters);
                }
            case BetweenOperator between:
                return TranslateBetweenFilter(between);
            case InOperator inOperator:
                return TranslateInFilter(inOperator);
            case UnaryOperator unary when unary.OperatorType == UnaryOperatorType.Not:
                {
                    var inner = TranslateMatchInternal(unary.Operand);
                    if (inner != null && inner.ElementCount > 0)
                    {
                        return new BsonDocument("$nor", new BsonArray { inner });
                    }

                    var expr = TranslateExpression(unary.Operand).Value;
                    return new BsonDocument("$expr", new BsonDocument("$not", expr));
                }
            default:
                return null;
        }
    }

    private bool TryBuildComparisonFilter(BinaryOperatorType type, MongoExpression left, MongoExpression right, out BsonDocument filter)
    {
        filter = null;
        if (!IsComparison(type))
        {
            return false;
        }

        if (left.IsLetReference || right.IsLetReference)
        {
            return false;
        }

        if (left.IsField && !left.IsLetReference && right.IsConstant)
        {
            filter = BuildFieldComparison(left.GetFieldName(), type, right.Value);
            return true;
        }

        if (right.IsField && !right.IsLetReference && left.IsConstant)
        {
            filter = BuildFieldComparison(right.GetFieldName(), Reverse(type), left.Value);
            return true;
        }

        return false;
    }

    private static BinaryOperatorType Reverse(BinaryOperatorType type)
    {
        switch (type)
        {
            case BinaryOperatorType.Greater:
                return BinaryOperatorType.Less;
            case BinaryOperatorType.GreaterOrEqual:
                return BinaryOperatorType.LessOrEqual;
            case BinaryOperatorType.Less:
                return BinaryOperatorType.Greater;
            case BinaryOperatorType.LessOrEqual:
                return BinaryOperatorType.GreaterOrEqual;
            default:
                return type;
        }
    }

    private static BsonDocument BuildFieldComparison(string field, BinaryOperatorType type, BsonValue value)
    {
        var doc = new BsonDocument();
        switch (type)
        {
            case BinaryOperatorType.Equal:
                doc[field] = value;
                break;
            case BinaryOperatorType.NotEqual:
                doc[field] = new BsonDocument("$ne", value);
                break;
            case BinaryOperatorType.Greater:
                doc[field] = new BsonDocument("$gt", value);
                break;
            case BinaryOperatorType.GreaterOrEqual:
                doc[field] = new BsonDocument("$gte", value);
                break;
            case BinaryOperatorType.Less:
                doc[field] = new BsonDocument("$lt", value);
                break;
            case BinaryOperatorType.LessOrEqual:
                doc[field] = new BsonDocument("$lte", value);
                break;
        }

        return doc;
    }

    private BsonDocument TranslateBetweenFilter(BetweenOperator between)
    {
        var test = TranslateExpression(between.TestExpression);
        var begin = TranslateExpression(between.BeginExpression);
        var end = TranslateExpression(between.EndExpression);
        if (test.IsField && begin.IsConstant && end.IsConstant)
        {
            return new BsonDocument(test.GetFieldName(), new BsonDocument
            {
                { "$gte", begin.Value },
                { "$lte", end.Value }
            });
        }

        var expr = new BsonDocument("$and", new BsonArray
        {
            new BsonDocument("$gte", new BsonArray { test.Value, begin.Value }),
            new BsonDocument("$lte", new BsonArray { test.Value, end.Value })
        });
        return new BsonDocument("$expr", expr);
    }

    private BsonDocument TranslateInFilter(InOperator inOperator)
    {
        var left = TranslateExpression(inOperator.LeftOperand);
        var list = new BsonArray();
        bool allConstants = true;
        foreach (var operand in inOperator.Operands)
        {
            var expr = TranslateExpression(operand);
            list.Add(expr.Value);
            allConstants &= expr.IsConstant;
        }

        if (left.IsField && allConstants)
        {
            return new BsonDocument(left.GetFieldName(), new BsonDocument("$in", list));
        }

        var exprDoc = new BsonDocument("$in", new BsonArray { left.Value, list });
        return new BsonDocument("$expr", exprDoc);
    }

    private static bool IsComparison(BinaryOperatorType type)
    {
        switch (type)
        {
            case BinaryOperatorType.Equal:
            case BinaryOperatorType.NotEqual:
            case BinaryOperatorType.Greater:
            case BinaryOperatorType.GreaterOrEqual:
            case BinaryOperatorType.Less:
            case BinaryOperatorType.LessOrEqual:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Translates a criteria operator into a Mongo expression usable in projections or <c>$expr</c> constructs.
    /// </summary>
    public MongoExpression TranslateExpression(CriteriaOperator criteria)
    {
        if (ReferenceEquals(criteria, null))
        {
            return MongoExpression.Constant(null);
        }

        if (groupMapping != null && groupMapping.TryResolveGroupValue(criteria, out var mapped))
        {
            return mapped;
        }

        switch (criteria)
        {
            case QueryOperand query:
                return ((IQueryCriteriaVisitor<MongoExpression>)this).Visit(query);
            case QuerySubQueryContainer subQuery:
                return ((IQueryCriteriaVisitor<MongoExpression>)this).Visit(subQuery);
            case OperandValue operandValue:
                return ((ICriteriaVisitor<MongoExpression>)this).Visit(operandValue);
            case OperandProperty operandProperty:
                if (groupMapping != null && groupMapping.TryResolveGroupValue(operandProperty, out var groupedProp))
                {
                    return groupedProp;
                }

                return MongoExpression.Field(scope.ResolveProperty(operandProperty.PropertyName));
            case BinaryOperator binary:
                return TranslateBinary(binary, TranslateExpression(binary.LeftOperand), TranslateExpression(binary.RightOperand));
            case BetweenOperator between:
                return TranslateBetween(between);
            case InOperator inOperator:
                return TranslateIn(inOperator);
            case GroupOperator group:
                return TranslateGroup(group);
            case UnaryOperator unary:
                return TranslateUnary(unary);
            case FunctionOperator function:
                return TranslateFunction(function);
            default:
                throw new NotSupportedException($"Criteria operator '{criteria.GetType().Name}' is not supported for Mongo translation.");
        }
    }

    private MongoExpression TranslateUnary(UnaryOperator unary)
    {
        var operand = TranslateExpression(unary.Operand);
        switch (unary.OperatorType)
        {
            case UnaryOperatorType.Not:
                return MongoExpression.Raw(new BsonDocument("$not", operand.Value));
            case UnaryOperatorType.Minus:
                return MongoExpression.Raw(new BsonDocument("$multiply", new BsonArray { -1, operand.Value }));
            case UnaryOperatorType.Plus:
                return operand;
            case UnaryOperatorType.BitwiseNot:
                return MongoExpression.Raw(new BsonDocument("$bitNot", operand.Value));
            case UnaryOperatorType.IsNull:
                return MongoExpression.Raw(new BsonDocument("$eq", new BsonArray { operand.Value, BsonNull.Value }));
            default:
                return operand;
        }
    }

    private MongoExpression TranslateGroup(GroupOperator group)
    {
        var operands = new BsonArray();
        foreach (var operand in group.Operands)
        {
            var expr = TranslateExpression(operand);
            operands.Add(expr.Value);
        }

        var opName = group.OperatorType == GroupOperatorType.And ? "$and" : "$or";
        return MongoExpression.Raw(new BsonDocument(opName, operands));
    }

    private MongoExpression TranslateIn(InOperator inOperator)
    {
        var left = TranslateExpression(inOperator.LeftOperand);
        var values = new BsonArray(inOperator.Operands.Select(o => TranslateExpression(o).Value));
        return MongoExpression.Raw(new BsonDocument("$in", new BsonArray
        {
            left.Value,
            values
        }));
    }

    private MongoExpression TranslateBetween(BetweenOperator between)
    {
        var test = TranslateExpression(between.TestExpression);
        var begin = TranslateExpression(between.BeginExpression);
        var end = TranslateExpression(between.EndExpression);
        return MongoExpression.Raw(new BsonDocument("$and", new BsonArray
        {
            new BsonDocument("$gte", new BsonArray { test.Value, begin.Value }),
            new BsonDocument("$lte", new BsonArray { test.Value, end.Value })
        }));
    }

    private static bool ContainsLetReference(BsonDocument doc)
    {
        foreach (var element in doc.Elements)
        {
            if (ContainsLetReference(element.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsLetReference(BsonValue value)
    {
        if (value == null)
        {
            return false;
        }

        if (value.IsString && value.AsString.StartsWith("$$", StringComparison.Ordinal))
        {
            return true;
        }

        if (value is BsonDocument doc && ContainsLetReference(doc))
        {
            return true;
        }

        if (value is BsonArray array)
        {
            foreach (var item in array)
            {
                if (ContainsLetReference(item))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static BsonValue MatchDocumentToExpr(BsonDocument match)
    {
        if (match == null || match.ElementCount == 0)
        {
            return BsonBoolean.True;
        }

        if (match.TryGetValue("$expr", out var exprVal))
        {
            return exprVal;
        }

        if (match.Contains("$or"))
        {
            var converted = new BsonArray();
            foreach (var item in match["$or"].AsBsonArray)
            {
                if (item is BsonDocument itemDoc)
                {
                    converted.Add(MatchDocumentToExpr(itemDoc));
                }
                else
                {
                    converted.Add(item);
                }
            }

            return new BsonDocument("$or", converted);
        }

        if (match.Contains("$and"))
        {
            var converted = new BsonArray();
            foreach (var item in match["$and"].AsBsonArray)
            {
                if (item is BsonDocument itemDoc)
                {
                    converted.Add(MatchDocumentToExpr(itemDoc));
                }
                else
                {
                    converted.Add(item);
                }
            }

            return new BsonDocument("$and", converted);
        }

        if (match.Contains("$nor"))
        {
            var converted = new BsonArray();
            foreach (var item in match["$nor"].AsBsonArray)
            {
                if (item is BsonDocument itemDoc)
                {
                    converted.Add(MatchDocumentToExpr(itemDoc));
                }
                else
                {
                    converted.Add(item);
                }
            }

            return new BsonDocument("$not", new BsonDocument("$or", converted));
        }

        if (match.ElementCount > 1)
        {
            var clauses = new BsonArray();
            foreach (var element in match.Elements)
            {
                clauses.Add(ConvertFieldComparison(element.Name, element.Value));
            }

            return new BsonDocument("$and", clauses);
        }

        var single = match.GetElement(0);
        return ConvertFieldComparison(single.Name, single.Value);
    }

    private static BsonValue ConvertFieldComparison(string fieldName, BsonValue value)
    {
        var field = fieldName.StartsWith("$", StringComparison.Ordinal) ? fieldName : $"${fieldName}";
        if (value is BsonDocument opDoc)
        {
            if (opDoc.ElementCount == 1 && opDoc.GetElement(0).Name.StartsWith("$", StringComparison.Ordinal))
            {
                var opName = opDoc.GetElement(0).Name;
                var operand = opDoc.GetElement(0).Value;
                if (RequiresNotNullGuard(opName))
                {
                    return new BsonDocument("$and", new BsonArray
                    {
                        new BsonDocument(opName, new BsonArray { field, operand }),
                        new BsonDocument("$ne", new BsonArray { field, BsonNull.Value })
                    });
                }

                return new BsonDocument(opName, new BsonArray { field, operand });
            }

            var clauses = new BsonArray();
            foreach (var element in opDoc.Elements)
            {
                if (RequiresNotNullGuard(element.Name))
                {
                    clauses.Add(new BsonDocument("$and", new BsonArray
                    {
                        new BsonDocument(element.Name, new BsonArray { field, element.Value }),
                        new BsonDocument("$ne", new BsonArray { field, BsonNull.Value })
                    }));
                }
                else
                {
                    clauses.Add(new BsonDocument(element.Name, new BsonArray { field, element.Value }));
                }
            }

            return new BsonDocument("$and", clauses);
        }

        return new BsonDocument("$eq", new BsonArray { field, value });
    }

    private static bool RequiresNotNullGuard(string opName)
    {
        switch (opName)
        {
            case "$lt":
            case "$lte":
            case "$gt":
            case "$gte":
                return true;
            default:
                return false;
        }
    }

    private MongoExpression TranslateBinary(BinaryOperator binary, MongoExpression left, MongoExpression right)
    {
        switch (binary.OperatorType)
        {
            case BinaryOperatorType.Plus:
                return MongoExpression.Raw(new BsonDocument("$add", new BsonArray { left.Value, right.Value }));
            case BinaryOperatorType.Minus:
                return MongoExpression.Raw(new BsonDocument("$subtract", new BsonArray { left.Value, right.Value }));
            case BinaryOperatorType.Multiply:
                return MongoExpression.Raw(new BsonDocument("$multiply", new BsonArray { left.Value, right.Value }));
            case BinaryOperatorType.Divide:
                return MongoExpression.Raw(new BsonDocument("$divide", new BsonArray { left.Value, right.Value }));
            case BinaryOperatorType.Modulo:
                return MongoExpression.Raw(new BsonDocument("$mod", new BsonArray { left.Value, right.Value }));
            case BinaryOperatorType.BitwiseAnd:
                return MongoExpression.Raw(new BsonDocument("$bitAnd", new BsonArray { left.Value, right.Value }));
            case BinaryOperatorType.BitwiseOr:
                return MongoExpression.Raw(new BsonDocument("$bitOr", new BsonArray { left.Value, right.Value }));
            case BinaryOperatorType.BitwiseXor:
                return MongoExpression.Raw(new BsonDocument("$bitXor", new BsonArray { left.Value, right.Value }));
            case BinaryOperatorType.Less:
                return Compare("$lt", left, right);
            case BinaryOperatorType.LessOrEqual:
                return Compare("$lte", left, right);
            case BinaryOperatorType.Greater:
                return Compare("$gt", left, right);
            case BinaryOperatorType.GreaterOrEqual:
                return Compare("$gte", left, right);
            case BinaryOperatorType.Equal:
                return Compare("$eq", left, right);
            case BinaryOperatorType.NotEqual:
                return Compare("$ne", left, right);
            case BinaryOperatorType.Like:
                return BuildLikeExpression(left, right);
            default:
                throw new NotSupportedException($"Binary operator '{binary.OperatorType}' is not supported.");
        }
    }

    private MongoExpression BuildLikeExpression(MongoExpression left, MongoExpression right)
    {
        string likePattern;
        var value = right.Value;

        if (value == null || value.IsBsonNull)
        {
            likePattern = string.Empty;
        }
        else if (value.IsString)
        {
            likePattern = value.AsString;
        }
        else
        {
            likePattern = value.ToString();
        }

        var regex = LikePatternToRegex(likePattern);
        return MongoExpression.Raw(BuildRegexMatch(left.Value, regex));
    }


    // Escape regex metacharacters
    private static string EscapeRegexLiteral(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var sb = new StringBuilder(text.Length * 2);
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '.':
                case '^':
                case '$':
                case '*':
                case '+':
                case '?':
                case '{':
                case '}':
                case '[':
                case ']':
                case '\\':
                case '|':
                case '(':
                case ')':
                    sb.Append('\\').Append(ch);
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }
        return sb.ToString();
    }

    // SQL/XPO LIKE ? anchored regex
    // % -> .*   _ -> .
    private static string LikePatternToRegex(string likePattern)
    {
        if (likePattern == null)
            likePattern = string.Empty;

        var sb = new StringBuilder(likePattern.Length * 2);
        sb.Append('^');

        foreach (var ch in likePattern)
        {
            switch (ch)
            {
                case '%':
                    sb.Append(".*");
                    break;
                case '_':
                    sb.Append('.');
                    break;
                default:
                    sb.Append(EscapeRegexLiteral(ch.ToString()));
                    break;
            }
        }

        sb.Append('$');
        return sb.ToString();
    }

    // Central place for $regexMatch + case sensitivity
    private static BsonDocument BuildRegexMatch(BsonValue input, string pattern)
    {
        var inner = new BsonDocument
        {
            { "input", input },
            { "regex", pattern }
        };

        // When XPO is NOT case-sensitive, explicitly add "i"
        if (!XpoDefault.DefaultCaseSensitive)
        {
            inner.Add("options", "i");
        }

        return new BsonDocument("$regexMatch", inner);
    }


    private static MongoExpression Compare(string opName, MongoExpression left, MongoExpression right)
    {
        return MongoExpression.Raw(new BsonDocument(opName, new BsonArray { left.Value, right.Value }));
    }

    private MongoExpression ProcessCustomFunctionOperator(FunctionOperator function, MongoExpression[] args)
    {
        // First operand is the function name as a string
        if (function.Operands.Count == 0 ||
            function.Operands[0] is not OperandValue nameOperand ||
            nameOperand.Value is not string funcName)
        {
            throw new NotSupportedException(
                $"Custom function with unexpected signature: {function}.");
        }


        // ---------------------------------
        // Special case: Like(Name, 'A%')
        // ---------------------------------
        if (string.Equals(funcName, "Like", StringComparison.OrdinalIgnoreCase))
        {
            if (function.Operands.Count < 3)
                throw new NotSupportedException("Like(name, pattern) requires 2 operands.");

            // args[] is the translated operands:
            // args[0] => "Like" (we ignore)
            // args[1] => target expression
            // args[2] => pattern
            var left = args[1];
            var right = args[2];

            return BuildLikeExpression(left, right); 
        }

        if (Enum.TryParse<FunctionOperatorType>(funcName, true, out var functionOperatorType))
        {
            // Re-wrap as a standard function:
            // we drop operand[0] (name) and pass the rest as operands
            var innerOperands = function.Operands.Skip(1).ToArray();
            var normalized = new FunctionOperator(functionOperatorType, innerOperands);

            // Important: call your existing FunctionOperator translation path,
            // NOT the "custom" path again, or you'll recurse.
            return TranslateFunction(normalized);
        }

        throw new NotSupportedException($"Function '{function.OperatorType}' with '{args[0].Value.AsString}' is not supported for Mongo translation.");
    }

    private MongoExpression TranslateFunction(FunctionOperator function)
    {
        if (EvalHelpers.TryExpandAndEvaluateIntrinsics(function, out var expanded))
        {
            return TranslateExpression(expanded);
        }

        var args = function.Operands.Select(TranslateExpression).ToArray();
        switch (function.OperatorType)
        {
            case FunctionOperatorType.Concat:
                return MongoExpression.Raw(new BsonDocument("$concat", new BsonArray(args.Select(a => a.Value))));
            case FunctionOperatorType.Lower:
                return MongoExpression.Raw(new BsonDocument("$toLower", args[0].Value));
            case FunctionOperatorType.Upper:
                return MongoExpression.Raw(new BsonDocument("$toUpper", args[0].Value));
            case FunctionOperatorType.Trim:
                return MongoExpression.Raw(new BsonDocument("$trim", new BsonDocument("input", args[0].Value)));
            case FunctionOperatorType.IsNullOrEmpty:
                return MongoExpression.Raw(new BsonDocument("$or", new BsonArray
                {
                    new BsonDocument("$eq", new BsonArray { args[0].Value, BsonNull.Value }),
                    new BsonDocument("$eq", new BsonArray { args[0].Value, string.Empty })
                }));
            case FunctionOperatorType.Len:
                {
                    // SQL LEN ignores trailing spaces: keep leading, drop trailing.
                    var totalLen = new BsonDocument("$strLenCP", args[0].Value);
                    var ltrimLen = new BsonDocument("$strLenCP", new BsonDocument("$ltrim", new BsonDocument
                    {
                        { "input", args[0].Value },
                        { "chars", " " }
                    }));
                    var trimmedLen = new BsonDocument("$strLenCP", new BsonDocument("$trim", new BsonDocument
                    {
                        { "input", args[0].Value },
                        { "chars", " " }
                    }));
                    var leadingSpaces = new BsonDocument("$subtract", new BsonArray { totalLen, ltrimLen });
                    return MongoExpression.Raw(new BsonDocument("$add", new BsonArray { trimmedLen, leadingSpaces }));
                }
            case FunctionOperatorType.Substring:
                {
                    var parameters = new BsonArray { args[0].Value };
                    if (args.Length > 1)
                    {
                        parameters.Add(args[1].Value);
                    }

                    if (args.Length > 2)
                    {
                        parameters.Add(args[2].Value);
                    }

                    return MongoExpression.Raw(new BsonDocument("$substrCP", parameters));
                }
            case FunctionOperatorType.ToStr:
                return MongoExpression.Raw(new BsonDocument("$toString", args[0].Value));
            case FunctionOperatorType.Replace:
                {
                    var doc = new BsonDocument("$replaceAll", new BsonDocument
                    {
                        { "input", args[0].Value },
                        { "find", args.Length > 1 ? args[1].Value : string.Empty },
                        { "replacement", args.Length > 2 ? args[2].Value : string.Empty }
                    });
                    return MongoExpression.Raw(doc);
                }
            case FunctionOperatorType.Reverse:
                {
                    var split = new BsonDocument("$split", new BsonArray { args[0].Value, string.Empty });
                    var reversed = new BsonDocument("$reverseArray", split);
                    var reduce = new BsonDocument("$reduce", new BsonDocument
                    {
                        { "input", reversed },
                        { "initialValue", string.Empty },
                        { "in", new BsonDocument("$concat", new BsonArray { "$$value", "$$this" }) }
                    });
                    return MongoExpression.Raw(reduce);
                }
            case FunctionOperatorType.Insert:
                {
                    // input, position, insertString
                    var source = args[0].Value;
                    var pos = args.Length > 1 ? args[1].Value : 0;
                    var ins = args.Length > 2 ? args[2].Value : string.Empty;
                    var left = new BsonDocument("$substrCP", new BsonArray { source, 0, pos });
                    var right = new BsonDocument("$substrCP", new BsonArray { source, pos, new BsonDocument("$strLenCP", source) });
                    return MongoExpression.Raw(new BsonDocument("$concat", new BsonArray { left, ins, right }));
                }
            case FunctionOperatorType.Remove:
                {
                    var source = args[0].Value;
                    var start = args.Length > 1 ? args[1].Value : 0;
                    var length = args.Length > 2 ? args[2].Value : new BsonDocument("$strLenCP", source);
                    var left = new BsonDocument("$substrCP", new BsonArray { source, 0, start });
                    var rightStart = new BsonDocument("$add", new BsonArray { start, length });
                    var rightLength = new BsonDocument("$subtract", new BsonArray { new BsonDocument("$strLenCP", source), rightStart });
                    var right = new BsonDocument("$substrCP", new BsonArray { source, rightStart, rightLength });
                    return MongoExpression.Raw(new BsonDocument("$concat", new BsonArray { left, right }));
                }
            case FunctionOperatorType.CharIndex:
                {
                    var substr = new BsonDocument("$toLower", args[0].Value);
                    var source = new BsonDocument("$toLower", args.Length > 1 ? args[1].Value : string.Empty);
                    var startIndex = args.Length > 2 ? args[2].Value : null;
                    var indexArgs = new BsonArray { source, substr };
                    if (startIndex != null)
                    {
                        indexArgs.Add(startIndex);
                    }
                    return MongoExpression.Raw(new BsonDocument("$indexOfCP", indexArgs));
                }
            case FunctionOperatorType.Abs:
                return MongoExpression.Raw(new BsonDocument("$abs", args[0].Value));
            case FunctionOperatorType.Sign:
                return MongoExpression.Raw(new BsonDocument("$cmp", new BsonArray { args[0].Value, 0 }));
            case FunctionOperatorType.Round:
                {
                    var roundArgs = new BsonArray { args[0].Value };
                    if (args.Length > 1)
                    {
                        roundArgs.Add(args[1].Value);
                    }
                    return MongoExpression.Raw(new BsonDocument("$round", roundArgs));
                }
            case FunctionOperatorType.Floor:
                return MongoExpression.Raw(new BsonDocument("$floor", args[0].Value));
            case FunctionOperatorType.Ceiling:
                return MongoExpression.Raw(new BsonDocument("$ceil", args[0].Value));
            case FunctionOperatorType.Sqr:
                return MongoExpression.Raw(new BsonDocument("$sqrt", args[0].Value));
            case FunctionOperatorType.Cos:
                return MongoExpression.Raw(new BsonDocument("$cos", args[0].Value));
            case FunctionOperatorType.Sin:
                return MongoExpression.Raw(new BsonDocument("$sin", args[0].Value));
            case FunctionOperatorType.Tan:
                return MongoExpression.Raw(new BsonDocument("$tan", args[0].Value));
            case FunctionOperatorType.Atn:
                return MongoExpression.Raw(new BsonDocument("$atan", args[0].Value));
            case FunctionOperatorType.Atn2:
                return MongoExpression.Raw(new BsonDocument("$atan2", new BsonArray { args[0].Value, args.Length > 1 ? args[1].Value : 0 }));
            case FunctionOperatorType.Acos:
                return MongoExpression.Raw(new BsonDocument("$acos", args[0].Value));
            case FunctionOperatorType.Asin:
                return MongoExpression.Raw(new BsonDocument("$asin", args[0].Value));
            case FunctionOperatorType.Cosh:
                return MongoExpression.Raw(new BsonDocument("$cosh", args[0].Value));
            case FunctionOperatorType.Sinh:
                return MongoExpression.Raw(new BsonDocument("$sinh", args[0].Value));
            case FunctionOperatorType.Tanh:
                return MongoExpression.Raw(new BsonDocument("$tanh", args[0].Value));
            case FunctionOperatorType.Exp:
                return MongoExpression.Raw(new BsonDocument("$exp", args[0].Value));
            case FunctionOperatorType.Log:
                if (args.Length == 1)
                {
                    return MongoExpression.Raw(new BsonDocument("$ln", args[0].Value));
                }
                return MongoExpression.Raw(new BsonDocument("$divide", new BsonArray
                {
                    new BsonDocument("$ln", args[0].Value),
                    new BsonDocument("$ln", args[1].Value)
                }));
            case FunctionOperatorType.Log10:
                return MongoExpression.Raw(new BsonDocument("$log10", args[0].Value));
            case FunctionOperatorType.Power:
                return MongoExpression.Raw(new BsonDocument("$pow", new BsonArray { args[0].Value, args[1].Value }));
            case FunctionOperatorType.Rnd:
                return MongoExpression.Raw(new BsonDocument("$rand", new BsonDocument()));
            case FunctionOperatorType.BigMul:
                return MongoExpression.Raw(new BsonDocument("$multiply", new BsonArray { args[0].Value, args[1].Value }));
            case FunctionOperatorType.GetYear:
                return MongoExpression.Raw(new BsonDocument("$year", args[0].Value));
            case FunctionOperatorType.GetMonth:
                return MongoExpression.Raw(new BsonDocument("$month", args[0].Value));
            case FunctionOperatorType.GetDay:
                return MongoExpression.Raw(new BsonDocument("$dayOfMonth", args[0].Value));
            case FunctionOperatorType.GetHour:
                return MongoExpression.Raw(new BsonDocument("$hour", args[0].Value));
            case FunctionOperatorType.GetMinute:
                return MongoExpression.Raw(new BsonDocument("$minute", args[0].Value));
            case FunctionOperatorType.GetSecond:
                return MongoExpression.Raw(new BsonDocument("$second", args[0].Value));
            case FunctionOperatorType.GetMilliSecond:
                return MongoExpression.Raw(new BsonDocument("$millisecond", args[0].Value));
            case FunctionOperatorType.GetDayOfWeek:
                return MongoExpression.Raw(new BsonDocument("$dayOfWeek", args[0].Value));
            case FunctionOperatorType.GetDayOfYear:
                return MongoExpression.Raw(new BsonDocument("$dayOfYear", args[0].Value));
            case FunctionOperatorType.Iif:
                {
                    if (args.Length < 3 || args.Length % 2 == 0)
                    {
                        throw new InvalidOperationException("Iif requires condition, true and false branches.");
                    }

                    // Multi-branch Iif: Iif(c1, t1, c2, t2, ..., else)
                    var currentElse = args[args.Length - 1];
                    for (int i = args.Length - 3; i >= 0; i -= 2)
                    {
                        var condition = args[i];
                        var thenBranch = args[i + 1];
                        var condDoc = new BsonDocument
                        {
                            { "if", condition.Value },
                            { "then", thenBranch.Value },
                            { "else", currentElse.Value }
                        };
                        currentElse = MongoExpression.Raw(new BsonDocument("$cond", condDoc));
                    }

                    return currentElse;
                }
            case FunctionOperatorType.IsNull:
                return MongoExpression.Raw(new BsonDocument("$ifNull", new BsonArray
                {
                    args[0].Value,
                    args.Length > 1 ? args[1].Value : BsonNull.Value
                }));
            case FunctionOperatorType.Contains:
                {
                    var pattern = ToPattern(args.Length > 1 ? args[1].Value : BsonValue.Create(string.Empty));
                    return MongoExpression.Raw(BuildRegexMatch(args[0].Value, pattern));
                }
            case FunctionOperatorType.StartsWith:
                {
                    var pattern = "^" + ToPattern(args.Length > 1 ? args[1].Value : BsonValue.Create(string.Empty));
                    return MongoExpression.Raw(BuildRegexMatch(args[0].Value, pattern));
                }
            case FunctionOperatorType.EndsWith:
                {
                    var pattern = ToPattern(args.Length > 1 ? args[1].Value : BsonValue.Create(string.Empty)) + "$";
                    return MongoExpression.Raw(BuildRegexMatch(args[0].Value, pattern));
                }
            case FunctionOperatorType.ToInt:
                {
                    var input = args[0].Value;
                    var rounded = new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$gt", new BsonArray { input, 0 }),
                        new BsonDocument("$floor", new BsonDocument("$add", new BsonArray { input, 0.5 })),
                        new BsonDocument("$ceil", new BsonDocument("$subtract", new BsonArray { input, 0.5 }))
                    });
                    return MongoExpression.Raw(new BsonDocument("$toInt", rounded));
                }
            case FunctionOperatorType.ToLong:
                {
                    var input = args[0].Value;
                    var rounded = new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$gt", new BsonArray { input, 0 }),
                        new BsonDocument("$floor", new BsonDocument("$add", new BsonArray { input, 0.5 })),
                        new BsonDocument("$ceil", new BsonDocument("$subtract", new BsonArray { input, 0.5 }))
                    });
                    return MongoExpression.Raw(new BsonDocument("$toLong", rounded));
                }
            case FunctionOperatorType.ToDouble:
            case FunctionOperatorType.ToFloat:
                return MongoExpression.Raw(new BsonDocument("$toDouble", args[0].Value));
            case FunctionOperatorType.ToDecimal:
                return MongoExpression.Raw(new BsonDocument("$toDecimal", args[0].Value));
            case FunctionOperatorType.Custom:
                return ProcessCustomFunctionOperator(function, args);
            //    {
            //        var name = function.FunctionName?.Trim() ?? string.Empty;
            //        var nameLower = name.ToLowerInvariant();
            //        // Basic support for common custom patterns such as NotLike/Like.
            //        if (nameLower == "notlike" || nameLower == "not like")
            //        {
            //            var input = args.Length > 0 ? args[0].Value : BsonNull.Value;
            //            var pattern = "^" + ToPattern(args.Length > 1 ? args[1].Value : BsonValue.Create(string.Empty)) + "$";
            //            var regex = new BsonDocument("$regexMatch", new BsonDocument
            //            {
            //                { "input", input },
            //                { "regex", pattern },
            //                { "options", "i" }
            //            });
            //            return MongoExpression.Raw(new BsonDocument("$not", regex));
            //        }
            //        if (nameLower == "like")
            //        {
            //            var input = args.Length > 0 ? args[0].Value : BsonNull.Value;
            //            var pattern = "^" + ToPattern(args.Length > 1 ? args[1].Value : BsonValue.Create(string.Empty)) + "$";
            //            return MongoExpression.Raw(new BsonDocument("$regexMatch", new BsonDocument
            //            {
            //                { "input", input },
            //                { "regex", pattern },
            //                { "options", "i" }
            //            }));
            //        }
            //        throw new NotSupportedException($"Custom function '{function.FunctionName}' is not supported for Mongo translation.");
            //    }
            default:
                throw new NotSupportedException($"Function '{function.OperatorType}' is not supported for Mongo translation.");
        }
    }

    private static string ToPattern(BsonValue value)
    {
        if (value == null || value.IsBsonNull)
        {
            return string.Empty;
        }

        if (value.IsString)
        {
            return value.AsString;
        }

        return value.ToString();
    }

    MongoExpression ICriteriaVisitor<MongoExpression>.Visit(OperandValue theOperand)
    {
        return MongoExpression.Constant(theOperand.Value);
    }

    MongoExpression IQueryCriteriaVisitor<MongoExpression>.Visit(QueryOperand operand)
    {
        if (groupMapping != null && groupMapping.TryResolveGroupValue(operand, out var grouped))
        {
            return grouped;
        }

        return MongoExpression.Field(scope.Resolve(operand));
    }

    MongoExpression ICriteriaVisitor<MongoExpression>.Visit(BetweenOperator theOperator)
    {
        return TranslateBetween(theOperator);
    }

    MongoExpression ICriteriaVisitor<MongoExpression>.Visit(BinaryOperator theOperator)
    {
        return TranslateBinary(theOperator, TranslateExpression(theOperator.LeftOperand), TranslateExpression(theOperator.RightOperand));
    }

    MongoExpression ICriteriaVisitor<MongoExpression>.Visit(InOperator theOperator)
    {
        return TranslateIn(theOperator);
    }

    MongoExpression ICriteriaVisitor<MongoExpression>.Visit(GroupOperator theOperator)
    {
        return TranslateGroup(theOperator);
    }

    MongoExpression ICriteriaVisitor<MongoExpression>.Visit(UnaryOperator theOperator)
    {
        return TranslateUnary(theOperator);
    }

    MongoExpression ICriteriaVisitor<MongoExpression>.Visit(FunctionOperator theOperator)
    {
        return TranslateFunction(theOperator);
    }

    MongoExpression IQueryCriteriaVisitor<MongoExpression>.Visit(QuerySubQueryContainer container)
    {
        if (container.Node == null)
        {
            if (groupMapping != null && groupMapping.TryResolveAggregate(container, out var aggregateExpr))
            {
                return aggregateExpr;
            }

            throw new InvalidOperationException("Aggregate container requires grouping context.");
        }

        var alias = context.SubqueryPlanner.EnsureLookup(container, scope);
        return MongoExpression.Field($"${alias}");
    }
}


