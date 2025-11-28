// Part of the XpoNoSql.MongoDatabase provider.
// This file implements select translation orchestration into MongoDB aggregation plans as part of the XPO → MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using DevExpress.Xpo.DB;

using MongoDB.Bson;

using System;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Orchestrates translation of a <see cref="SelectStatement"/> into a MongoDB aggregation plan,
/// mirroring the SQL provider pipeline (joins, subqueries, where, group/having, projection, sort, paging).
/// </summary>
public sealed class MongoSelectTranslator
{
    /// <summary>
    /// Last translation context produced by <see cref="Translate"/>; useful for diagnostics/materialization.
    /// </summary>
    public MongoTranslationContext LastContext { get; private set; }

    /// <summary>
    /// Translates the given SelectStatement into a Mongo aggregation plan.
    /// </summary>
    public MongoAggregationPlan Translate(SelectStatement statement, IList<string> propertyAliases = null, MongoExpressionScope scope = null)
    {
        if (statement == null)
        {
            throw new ArgumentNullException(nameof(statement));
        }

        var aliases = new MongoAliasRegistry(statement.Alias ?? string.Empty, statement.Table.Name);
        var plan = new MongoAggregationPlan(statement.Table.Name);
        var baseScope = scope ?? new MongoExpressionScope(aliases);
        var context = new MongoTranslationContext(statement, plan, aliases, baseScope, propertyAliases);
        var criteriaTranslator = new MongoCriteriaTranslator(context, baseScope);
        LastContext = context;

        var joinBuilder = new MongoJoinBuilder(context, criteriaTranslator);
        joinBuilder.BuildInto(plan);

        context.SubqueryPlanner.PlanForStatement(statement);
        plan.AddStages(context.SubqueryPlanner.Stages);

        var where = criteriaTranslator.TranslateMatch(statement.Condition);
        if (where != null)
        {
            plan.AddStage("$match", where);
        }

        var groupBuilder = new MongoGroupBuilder(context, criteriaTranslator);
        groupBuilder.Collect(statement);
        var groupMapping = groupBuilder.Build();
        context.GroupMapping = groupMapping;

        if (groupMapping.HasGrouping) 
        {
            plan.AddStage(groupMapping.GroupStage);
        }

        var groupedTranslator = criteriaTranslator.WithGroup(groupMapping);
        var having = groupedTranslator.TranslateMatch(statement.GroupCondition);
        if (having != null)
        {
            plan.AddStage("$match", having);
        }

        var projectionBuilder = new MongoProjectionBuilder(context, groupMapping, groupedTranslator);
        var projection = projectionBuilder.Build(statement.Operands, statement.SortProperties, propertyAliases);
        plan.AddStage("$project", projection.ProjectStage);

        var sortStage = MongoSortBuilder.Build(statement.SortProperties, projection, groupMapping);
        if (sortStage != null)
        {
            plan.AddStage(sortStage);
        }

        if (projection.FinalStage != null)
        {
            plan.AddStage("$project", projection.FinalStage);
        }

        if (statement.SkipSelectedRecords > 0)
        {
            plan.AddStage("$skip", new BsonInt32(statement.SkipSelectedRecords));
        }

        if (statement.TopSelectedRecords > 0)
        {
            plan.AddStage("$limit", new BsonInt32(statement.TopSelectedRecords));
        }

        return plan;
    }
}


