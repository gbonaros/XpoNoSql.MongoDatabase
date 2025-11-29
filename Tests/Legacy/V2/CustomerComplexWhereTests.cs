
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class CustomerComplexWhereTests
{
    private readonly DbFixture _fx;
    public CustomerComplexWhereTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Filter_Customers_With_Complex_And_Or_Predicates()
    {
        _fx.Cleanup<TestCustomer>();
        _fx.Cleanup<TestOrder>();
        using (UnitOfWork uow = _fx.NewUow())
        {
            // -----------------------------
            // Test data
            // -----------------------------
            var c1 = new TestCustomer(uow) { Name = "Alice", Email = "alice@example.com" };
            var c2 = new TestCustomer(uow) { Name = "Alex", Email = "alex@foo.com" };
            var c3 = new TestCustomer(uow) { Name = "Bob", Email = "bob@example.com" };
            var c4 = new TestCustomer(uow) { Name = "Charlie", Email = "charlie@test.org" };
            var c5 = new TestCustomer(uow) { Name = "ALBERT", Email = "albert@example.com" };
            uow.CommitChanges();

            // condition:
            // (Name starts with "Al" OR Name starts with "AL")
            // AND Email ends with "example.com"
            var result = uow.Query<TestCustomer>()
                .Where(c =>
                    (c.Name.StartsWith("Al") || c.Name.StartsWith("AL")) &&
                    c.Email.EndsWith("@example.com"))
                .OrderBy(c => c.Name)
                .ToArray();

            // -----------------------------
            // Assertions
            // -----------------------------
            result.Should().HaveCount(2);
            result.Select(c => c.Name)
                  .Should().ContainInOrder("ALBERT", "Alice");
        }
    }

    [Fact]
    public void Filter_Customers_By_Null_And_NotNull_Email()
    {
        _fx.Cleanup<TestCustomer>();
        _fx.Cleanup<TestOrder>();

        using (UnitOfWork uow = _fx.NewUow())
        {
            // -----------------------------
            // Test data
            // -----------------------------
            var c1 = new TestCustomer(uow) { Name = "Alice", Email = "alice@example.com" };
            var c2 = new TestCustomer(uow) { Name = "Bob", Email = null! };
            var c3 = new TestCustomer(uow) { Name = "Carol" }; // default string.Empty if you kept that
            uow.CommitChanges();

            // treat null or empty as "missing email"
            var noEmail = uow.Query<TestCustomer>()
                .Where(c => c.Email == null || c.Email == string.Empty)
                .OrderBy(c => c.Name)
                .ToArray();

            var withEmail = uow.Query<TestCustomer>()
                .Where(c => c.Email != null && c.Email != string.Empty)
                .OrderBy(c => c.Name)
                .ToArray();

            // -----------------------------
            // Assertions
            // -----------------------------
            noEmail.Select(c => c.Name).Should().BeEquivalentTo("Bob", "Carol");
            withEmail.Select(c => c.Name).Should().BeEquivalentTo("Alice");
        }
    }

    private static void Cleanup(UnitOfWork uow)
    {
        foreach (var obj in new XPCollection<TestOrder>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<TestCustomer>(uow).ToList()) obj.Delete();
        uow.CommitChanges();
    }
}
