
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class OrderJoinTests
{
    private readonly DbFixture _fx;
    public OrderJoinTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Filter_Orders_By_CustomerName_And_Sort_By_Total_Desc()
    {
        using (UnitOfWork uow = _fx.NewUow())
        {
            Cleanup(uow);

            // -----------------------------
            // Test data
            // -----------------------------
            var alice = new TestCustomer(uow) { Name = "Alice" };
            var bob = new TestCustomer(uow) { Name = "Bob" };

            var o1 = new TestOrder(uow)
            {
                Customer = alice,
                ProductName = "Pencil",
                Quantity = 1,
                Total = 1.00m
            };
            var o2 = new TestOrder(uow)
            {
                Customer = alice,
                ProductName = "Notebook",
                Quantity = 2,
                Total = 5.00m
            };
            var o3 = new TestOrder(uow)
            {
                Customer = bob,
                ProductName = "Eraser",
                Quantity = 3,
                Total = 3.00m
            };
            uow.CommitChanges();

            // -----------------------------
            // The query under test
            // -----------------------------
            var result = uow.Query<TestOrder>()
                .Where(o => o.Customer != null && o.Customer.Name == "Alice")
                .OrderByDescending(o => o.Total)
                .ThenBy(o => o.ProductName)
                .Select(o => new
                {
                    CustomerName = o.Customer!.Name,
                    o.ProductName,
                    o.Total
                })
                .ToArray();

            // -----------------------------
            // Assertions
            // -----------------------------
            result.Should().HaveCount(2);

            result[0].CustomerName.Should().Be("Alice");
            result[0].ProductName.Should().Be("Notebook");
            result[0].Total.Should().Be(5.00m);

            result[1].CustomerName.Should().Be("Alice");
            result[1].ProductName.Should().Be("Pencil");
            result[1].Total.Should().Be(1.00m);
        }
    }

    [Fact]
    public void Filter_Customers_Having_Any_Order_With_Quantity_Greater_Than()
    {
        using (UnitOfWork uow = _fx.NewUow())
        {
            Cleanup(uow);

            // -----------------------------
            // Test data
            // -----------------------------
            var alice = new TestCustomer(uow) { Name = "Alice" };
            var bob = new TestCustomer(uow) { Name = "Bob" };

            new TestOrder(uow)
            {
                Customer = alice,
                ProductName = "Item1",
                Quantity = 1,
                Total = 10m
            };
            new TestOrder(uow)
            {
                Customer = alice,
                ProductName = "Item2",
                Quantity = 5,
                Total = 50m
            };
            new TestOrder(uow)
            {
                Customer = bob,
                ProductName = "Item3",
                Quantity = 2,
                Total = 20m
            };
            uow.CommitChanges();

            int threshold = 3;

            // -----------------------------
            // The query under test
            // -----------------------------
            var result = uow.Query<TestCustomer>()
                .Where(c => c.Orders.Any(o => o.Quantity >= threshold))
                .OrderBy(c => c.Name)
                .ToArray();

            // -----------------------------
            // Assertions
            // -----------------------------
            result.Should().HaveCount(1);
            result.Single().Name.Should().Be("Alice");
        }
    }

    private static void Cleanup(UnitOfWork uow)
    {
        foreach (var obj in new XPCollection<TestOrder>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<TestCustomer>(uow).ToList()) obj.Delete();
        uow.CommitChanges();
    }
}
