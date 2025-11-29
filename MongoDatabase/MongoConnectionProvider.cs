using DevExpress.Xpo.DB;
using DevExpress.Xpo.DB.Helpers;
using System;
using System.Data;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Thin connection helper that exposes the new <see cref="MongoDBDataStore"/> through the traditional XPO APIs.
/// </summary>
public static class MongoConnectionProvider
{
    /// <summary>
    /// Provider key that can be used inside connection strings (<c>XpoProvider=MongoDB</c>).
    /// </summary>
    public const string XpoProviderTypeString = "MongoDB";

    /// <summary>
    /// Creates a MongoDB-backed <see cref="IDataStore"/> from a raw connection string.
    /// </summary>
    public static IDataStore CreateProviderFromString(
        string connectionString,
        AutoCreateOption autoCreateOption,
        out IDisposable[] objectsToDisposeOnDisconnect)
    {
        IDataStore provider = new MongoDataStore(connectionString, autoCreateOption);
        objectsToDisposeOnDisconnect = Array.Empty<IDisposable>();
        return provider;
    }

    /// <summary>
    /// Builds a MongoDB connection string that references the V2 provider.
    /// Optional collation parameters can be supplied to keep case/locale stable across clients.
    /// </summary>
    public static string GetConnectionString(string connectionUri, string database, bool? caseSensitive = null, string collationLocale = null)
    {
        if (string.IsNullOrWhiteSpace(connectionUri))
        {
            throw new ArgumentException("Connection URI cannot be empty.", nameof(connectionUri));
        }

        if (string.IsNullOrWhiteSpace(database))
        {
            throw new ArgumentException("Database cannot be empty.", nameof(database));
        }

        var parts = new List<string>
        {
            $"XpoProvider={XpoProviderTypeString}",
            $"Data Source={ConnectionProviderSql.EscapeConnectionStringArgument(connectionUri)}",
            $"Database={ConnectionProviderSql.EscapeConnectionStringArgument(database)}"
        };

        if (caseSensitive.HasValue)
        {
            parts.Add($"CaseSensitive={caseSensitive.Value}");
        }

        if (!string.IsNullOrWhiteSpace(collationLocale))
        {
            parts.Add($"CollationLocale={ConnectionProviderSql.EscapeConnectionStringArgument(collationLocale)}");
        }

        return string.Join(';', parts) + ";";
    }

    /// <summary>
    /// Registers the V2 provider factory so it can be resolved by XPO using <see cref="XpoProviderTypeString"/>.
    /// </summary>
    public static void Register()
    {
        DataStoreBase.RegisterDataStoreProvider(
            XpoProviderTypeString,
            new DataStoreCreationFromStringDelegate(CreateProviderFromString));
    }
}
