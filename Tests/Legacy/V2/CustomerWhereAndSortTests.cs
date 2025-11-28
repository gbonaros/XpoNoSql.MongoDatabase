
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class CustomerWhereAndSortTests
{
    private readonly DbFixture _fx;
    public CustomerWhereAndSortTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Filter_Customers_By_NamePrefix_OrderedByName()
    {
        using (UnitOfWork uow = _fx.NewUow())
        {
            Cleanup(uow);

            // -----------------------------
            // Test data
            // -----------------------------
            var c1 = new TestCustomer(uow) { Name = "Alice", Email = "alice@example.com" };
            var c2 = new TestCustomer(uow) { Name = "Alex", Email = "alex@example.com" };
            var c3 = new TestCustomer(uow) { Name = "Bob", Email = "bob@example.com" };
            var c4 = new TestCustomer(uow) { Name = "Charlie", Email = "charlie@example.com" };
            uow.CommitChanges();

            // -----------------------------
            // The query under test
            // -----------------------------
            var result = uow.Query<TestCustomer>()
                .Where(c => c.Name.StartsWith("Al"))
                .OrderBy(c => c.Name)
                .ToArray();

            // -----------------------------
            // Assertions
            // -----------------------------
            result.Should().HaveCount(2);
            result.Select(c => c.Name).Should().ContainInOrder("Alex", "Alice");
        }
    }

    [Fact]
    public void Filter_Customers_By_EmailDomain_OrderedByEmail()
    {
        using (UnitOfWork uow = _fx.NewUow())
        {
            Cleanup(uow);

            // -----------------------------
            // Test data
            // -----------------------------
            var c1 = new TestCustomer(uow) { Name = "Alice", Email = "alice@gmail.com" };
            var c2 = new TestCustomer(uow) { Name = "Bob", Email = "bob@example.com" };
            var c3 = new TestCustomer(uow) { Name = "Charlie", Email = "charlie@gmail.com" };
            uow.CommitChanges();

            // -----------------------------
            // The query under test
            // -----------------------------
            var result = uow.Query<TestCustomer>()
                .Where(c => c.Email.EndsWith("@gmail.com"))
                .OrderBy(c => c.Email)
                .ToArray();

            // -----------------------------
            // Assertions
            // -----------------------------
            result.Should().HaveCount(2);
            result.Select(c => c.Email)
                  .Should().ContainInOrder("alice@gmail.com", "charlie@gmail.com");
        }
    }

    private static void Cleanup(UnitOfWork uow)
    {
        foreach (var obj in new XPCollection<TestOrder>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<TestCustomer>(uow).ToList()) obj.Delete();
        uow.CommitChanges();
    }
}
