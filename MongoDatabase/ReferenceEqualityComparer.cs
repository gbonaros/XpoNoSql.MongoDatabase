// Part of the XpoNoSql.MongoDatabase provider.
// This file implements a reference-only equality comparer helper used by planners as part of the XPO → MongoDB translation pipeline.
// Logic here must remain consistent with the rest of the provider.
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace XpoNoSQL.MongoDatabase.Core
{
    /// <summary>
    /// Provides reference-based equality semantics for dictionary/set usage.
    /// </summary>
    public sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

        public bool Equals(T x, T y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(T obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}

