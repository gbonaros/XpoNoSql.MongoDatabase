using Xunit;

namespace XpoNoSql.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class XpoCollection : ICollectionFixture<DbFixture>
{
    public const string Name = "Xpo Collection";
}
