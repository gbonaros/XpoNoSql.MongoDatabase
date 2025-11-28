// Part of the XpoNoSql.MongoDatabase provider.
// This file implements connection string parsing and normalization for the Mongo data store as part of the XPO → MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using System;
using System.Linq;

namespace XpoNoSQL.MongoDatabase.Core;

/// <summary>
/// Represents parsed connection options for the Mongo data store.
/// Supports Mongo URI or XPO-style key=value strings.
/// </summary>
public sealed record MongoConnectionOptions(string MongoUrl, string DatabaseName)
{
    /// <summary>
    /// Parses a connection string into Mongo URL and database name components.
    /// </summary>
    public static MongoConnectionOptions Parse(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required.", nameof(connectionString));

        // Fast path: raw MongoDB URI ("mongodb://..." or "mongodb+srv://...")
        if (connectionString.StartsWith("mongodb://", StringComparison.OrdinalIgnoreCase) ||
            connectionString.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase))
        {
            // Try to extract database name from the URI path: mongodb://host/dbName?opt=...
            int schemeEnd = connectionString.IndexOf("://", StringComparison.Ordinal);
            int firstSlash = connectionString.IndexOf('/', schemeEnd + 3);
            if (firstSlash < 0 || firstSlash == connectionString.Length - 1)
                throw new ArgumentException("Database parameter is required.", nameof(connectionString));

            string afterSlash = connectionString.Substring(firstSlash + 1);
            int endIdx = afterSlash.IndexOfAny(new[] { '?', '/', ' ' });
            string dbFromUri = (endIdx >= 0 ? afterSlash[..endIdx] : afterSlash).Trim();

            if (string.IsNullOrEmpty(dbFromUri))
                throw new ArgumentException("Database parameter is required.", nameof(connectionString));

            return new MongoConnectionOptions(connectionString, dbFromUri);
        }

        // XPO-style "key=value;key=value" connection string
        var parts = connectionString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string part in parts)
        {
            int eq = part.IndexOf('=');
            if (eq <= 0) // no key or no '='
                continue;

            string key = part.Substring(0, eq).Trim();
            string value = part[(eq + 1)..].Trim();

            if (key.Length == 0)
                continue;

            parsed[key] = value;
        }

        // Accept several common key names for the Mongo URL
        string uri;
        if (!parsed.TryGetValue("Data Source", out uri) &&
            !parsed.TryGetValue("DataSource", out uri) &&
            !parsed.TryGetValue("Server", out uri))
        {
            // Fallback: treat whole string as the URI
            uri = connectionString;
        }

        // Accept "Database" or "Initial Catalog"
        string database;
        if (!parsed.TryGetValue("Database", out database) &&
            !parsed.TryGetValue("Initial Catalog", out database))
        {
            throw new ArgumentException("Database parameter is required.", nameof(connectionString));
        }
        database = SafeDbName(database);
        return new MongoConnectionOptions(uri, database);
    }

    /// <summary>
    /// Sanitizes a database name by replacing invalid characters and enforcing length limits.
    /// </summary>
    public static string SafeDbName(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "db";

        const string invalid = "/\\.\"*<>:|?$";
        Span<char> buffer = stackalloc char[input.Length];
        int len = 0;

        foreach (char c in input)
        {
            buffer[len++] = invalid.Contains(c) ? '_' : c;
        }

        // MongoDB max name length = 63 chars
        if (len > 63)
            len = 63;

        return new string(buffer.Slice(0, len));
    }
}

