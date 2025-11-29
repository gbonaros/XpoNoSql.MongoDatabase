using System.Linq;

using DevExpress.Xpo;

using FluentAssertions;

using Xunit;


namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class CustomerOrderExistsAllTests
{
    private readonly DbFixture _fx;
    public CustomerOrderExistsAllTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Customers_With_Any_Order_Over_Threshold()
    {
        using (UnitOfWork uow = _fx.NewUow())
        {
            Cleanup(uow);

            // -----------------------------
            // Test data
            // -----------------------------
            var alice = new TestCustomer(uow) { Name = "Alice" };
            var bob = new TestCustomer(uow) { Name = "Bob" };
            var carol = new TestCustomer(uow) { Name = "Carol" };

            new TestOrder(uow) { Customer = alice, ProductName = "A1", Quantity = 1, Total = 10m };
            new TestOrder(uow) { Customer = alice, ProductName = "A2", Quantity = 1, Total = 100m };

            new TestOrder(uow) { Customer = bob, ProductName = "B1", Quantity = 1, Total = 20m };

            new TestOrder(uow) { Customer = carol, ProductName = "C1", Quantity = 1, Total = 200m };
            new TestOrder(uow) { Customer = carol, ProductName = "C2", Quantity = 1, Total = 250m };
            uow.CommitChanges();

            decimal threshold = 50m;

            // EXISTS (Orders where Total >= threshold)
            var result = uow.Query<TestCustomer>()
                .Where(c => c.Orders.Any(o => o.Total >= threshold))
                .OrderBy(c => c.Name)
                .ToArray();

            // -----------------------------
            // Assertions
            // -----------------------------
            result.Select(c => c.Name).Should().BeEquivalentTo("Alice", "Carol");
        }
    }

    [Fact]
    public void Customers_Where_All_Orders_Are_Above_Minimum()
    {
        using (UnitOfWork uow = _fx.NewUow())
        {
            Cleanup(uow);

            // -----------------------------
            // Test data
            // -----------------------------
            var alice = new TestCustomer(uow) { Name = "Alice" };
            var bob = new TestCustomer(uow) { Name = "Bob" };
            var carol = new TestCustomer(uow) { Name = "Carol" };

            // Alice: one small, one big -> fails All(...)
            new TestOrder(uow) { Customer = alice, ProductName = "A1", Quantity = 1, Total = 10m };
            new TestOrder(uow) { Customer = alice, ProductName = "A2", Quantity = 1, Total = 60m };

            // Bob: no orders -> All(...) is true on empty; we might want to exclude explicitly
            // to test both All and Any combos

            // Carol: all big -> should pass
            new TestOrder(uow) { Customer = carol, ProductName = "C1", Quantity = 1, Total = 70m };
            new TestOrder(uow) { Customer = carol, ProductName = "C2", Quantity = 1, Total = 80m };
            uow.CommitChanges();

            decimal minTotal = 50m;

            // "All orders over minTotal" AND "Has at least one order"
            var result = uow.Query<TestCustomer>()
                .Where(c =>
                    c.Orders.Any() &&
                    c.Orders.All(o => o.Total >= minTotal))
                .OrderBy(c => c.Name)
                .ToArray();

            // -----------------------------
            // Assertions
            // -----------------------------
            result.Should().HaveCount(1);
            result.Single().Name.Should().Be("Carol");
        }
    }

    private static void Cleanup(UnitOfWork uow)
    {
        foreach (var obj in new XPCollection<TestOrder>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<TestCustomer>(uow).ToList()) obj.Delete();
        uow.CommitChanges();
    }
}


