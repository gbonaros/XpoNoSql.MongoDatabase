using Xunit;

namespace XpoNoSQL.MongoDatabase.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class XpoCollection : ICollectionFixture<DbFixture>
{
    public const string Name = "Xpo Collection";
}
