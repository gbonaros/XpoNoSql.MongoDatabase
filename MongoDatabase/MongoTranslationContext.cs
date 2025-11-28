// Part of the XpoNoSql.MongoDatabase provider.
// This file implements translation context storage for select translation as part of the XPO → MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using DevExpress.Xpo.DB;

using System;

namespace XpoNoSQL.MongoDatabase.Core
{
    /// <summary>
    /// Holds per-translation state for a SelectStatement, including the working aggregation plan,
    /// alias registry, scope, grouping information, and subquery planner. Child contexts inherit from the parent.
    /// </summary>
    public sealed class MongoTranslationContext
    {
        /// <summary>
        /// The SelectStatement currently being translated.
        /// </summary>
        public SelectStatement Statement { get; }

        /// <summary>
        /// Aggregation plan being constructed for this statement.
        /// </summary>
        public MongoAggregationPlan Plan { get; }

        /// <summary>
        /// Tracks alias-to-path mappings for the current statement and joins.
        /// </summary>
        public MongoAliasRegistry Aliases { get; }

        /// <summary>
        /// Resolves operand fields and let variables in the current translation scope.
        /// </summary>
        public MongoExpressionScope Scope { get; }

        /// <summary>
        /// Plans correlated subqueries encountered during translation.
        /// </summary>
        public MongoSubqueryPlanner SubqueryPlanner { get; }

        /// <summary>
        /// Group mapping generated after grouping collection; empty when no grouping.
        /// </summary>
        public MongoGroupMapping GroupMapping { get; set; }

        /// <summary>
        /// Optional property aliases provided by caller (XPView, etc.).
        /// </summary>
        public IList<string> PropertyAliases { get; }

        /// <summary>
        /// Parent translation context, if this context was created for a subquery.
        /// </summary>
        public MongoTranslationContext Parent { get; }

        /// <summary>
        /// Initializes a new translation context for a SelectStatement.
        /// </summary>
        public MongoTranslationContext(SelectStatement statement, MongoAggregationPlan plan, MongoAliasRegistry aliases, MongoExpressionScope scope, IList<string> propertyAliases = null, MongoTranslationContext parent = null)
        {
            Statement = statement ?? throw new ArgumentNullException(nameof(statement));
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Aliases = aliases ?? throw new ArgumentNullException(nameof(aliases));
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            PropertyAliases = propertyAliases;
            Parent = parent;
            SubqueryPlanner = new MongoSubqueryPlanner(this);
            GroupMapping = MongoGroupMapping.Empty;
        }

        /// <summary>
        /// Creates a child context for translating a nested subquery, inheriting parent linkage.
        /// </summary>
        public MongoTranslationContext CreateChild(SelectStatement statement, MongoAliasRegistry aliases, MongoExpressionScope scope, IList<string> propertyAliases = null)
        {
            return new MongoTranslationContext(statement, new MongoAggregationPlan(statement.Table.Name), aliases, scope, propertyAliases, this);
        }
    }
}



