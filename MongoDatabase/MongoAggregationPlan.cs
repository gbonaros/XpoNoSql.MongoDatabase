// Part of the XpoNoSql.MongoDatabase provider.
// This file implements aggregation pipeline composition for translated SelectStatement instances as part of the XPO → MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using MongoDB.Bson;

using System;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Represents the aggregation pipeline produced by the translator for a SelectStatement.
/// Maintains the root collection name and ordered list of pipeline stages.
/// </summary>
public sealed class MongoAggregationPlan
{
    /// <summary>
    /// Root collection (MongoDB collection) targeted by the pipeline.
    /// </summary>
    public string RootCollection { get; }

    /// <summary>
    /// Ordered list of pipeline stages that will be executed.
    /// </summary>
    public IList<BsonDocument> Pipeline { get; } = new List<BsonDocument>();

    /// <summary>
    /// Initializes a new plan for the specified root collection.
    /// </summary>
    /// <param name="rootCollection">MongoDB collection targeted by the pipeline.</param>
    public MongoAggregationPlan(string rootCollection)
    {
        RootCollection = rootCollection ?? throw new ArgumentNullException(nameof(rootCollection));
    }

    /// <summary>
    /// Adds a stage with the given operator name and body.
    /// Skips null bodies to keep pipelines clean.
    /// </summary>
    public void AddStage(string stageName, BsonValue body)
    {
        if (body == null)
        {
            return;
        }

        Pipeline.Add(new BsonDocument(stageName, body));
    }

    /// <summary>
    /// Adds a prepared stage document to the pipeline if it is non-empty.
    /// </summary>
    /// <param name="stage">Stage document to append.</param>
    public void AddStage(BsonDocument stage)
    {
        if (stage == null || stage.ElementCount == 0)
        {
            return;
        }

        Pipeline.Add(stage);
    }

    /// <summary>
    /// Adds all provided stages, ignoring null/empty entries.
    /// </summary>
    public void AddStages(IEnumerable<BsonDocument> stages)
    {
        if (stages == null)
        {
            return;
        }

        foreach (var stage in stages)
        {
            AddStage(stage);
        }
    }
}
