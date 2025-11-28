// Part of the XpoNoSql.MongoDatabase provider.
// This file implements MongoDataStore construction and initialization as part of the XPO → MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using DevExpress.Xpo.DB;

using MongoDB.Driver;

using System;
using System.Linq;

namespace XpoNoSQL.MongoDatabase.Core;

public sealed partial class MongoDataStore : IDataStore
{
    /// <summary>
    /// Underlying MongoDB client used by this data store.
    /// </summary>
    internal MongoClient Client { get; set; }

    /// <summary>
    /// Initializes the MongoDataStore with the provided connection string and auto-create option.
    /// Parses connection settings, creates the Mongo client, and selects the target database.
    /// </summary>
    public MongoDataStore(string connectionString, AutoCreateOption autoCreateOption = AutoCreateOption.DatabaseAndSchema)
    {
        if (connectionString is null) throw new ArgumentNullException(nameof(connectionString));

        var options = MongoConnectionOptions.Parse(connectionString);

        Client = new MongoClient(options.MongoUrl);
        database = Client.GetDatabase(options.DatabaseName);
        this.autoCreateOption = autoCreateOption;
        ConnectionString = connectionString;

        //_tablesByName = new Dictionary<string, DBTable>(StringComparer.OrdinalIgnoreCase);
        //_selectTranslator = new MongoDBSelectTranslator();
    }
}


