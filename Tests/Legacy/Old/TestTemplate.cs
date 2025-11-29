using DevExpress.Xpo;

using FluentAssertions;

using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class TestTemplate
{
    private readonly DbFixture _fx;
    public TestTemplate(DbFixture fx) => _fx = fx;
    [Fact]
    public void SaveCustomer()
    {
        using (UnitOfWork uow = _fx.NewUow())
        {
            Cleanup(uow);
            // -----------------------------
            // Test data
            // -----------------------------

            var c1 = new TestCustomer(uow) { Name = "Alice" };
            var c2 = new TestCustomer(uow) { Name = "Bob" };
            var c3 = new TestCustomer(uow) { Name = "Charlie" };
            uow.CommitChanges();

            // -----------------------------
            // The query under test
            // -----------------------------

            var result = uow.Query<TestCustomer>()
                .ToArray();

            // -----------------------------
            // Assertions
            // -----------------------------

            result.Should().HaveCount(3);
        }
    }

    private static void Cleanup(UnitOfWork uow)
    {
        foreach (var obj in new XPCollection<TestCustomer>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<TestOrder>(uow).ToList()) obj.Delete();
        uow.CommitChanges();
    }
}
