
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class OrderGroupAggregateTests
{
    private readonly DbFixture _fx;
    public OrderGroupAggregateTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Group_Orders_By_Customer_And_Calculate_Count_And_Sum()
    {
        using (UnitOfWork uow = _fx.NewUow())
        {
            Cleanup(uow);

            // -----------------------------
            // Test data
            // -----------------------------
            var alice = new TestCustomer(uow) { Name = "Alice" };
            var bob = new TestCustomer(uow) { Name = "Bob" };

            new TestOrder(uow) { Customer = alice, ProductName = "Item1", Quantity = 1, Total = 10m };
            new TestOrder(uow) { Customer = alice, ProductName = "Item2", Quantity = 2, Total = 20m };
            new TestOrder(uow) { Customer = bob, ProductName = "Item3", Quantity = 3, Total = 30m };
            uow.CommitChanges();

            // -----------------------------
            // The query under test
            // -----------------------------
            var result = uow.Query<TestOrder>()
                .Where(o => o.Customer != null)
                .GroupBy(o => o.Customer!.Name)
                .Select(g => new
                {
                    CustomerName = g.Key,
                    OrderCount = g.Count(),
                    TotalAmount = g.Sum(o => o.Total)
                })
                .OrderBy(r => r.CustomerName)
                .ToArray();

            // -----------------------------
            // Assertions
            // -----------------------------
            result.Should().HaveCount(2);

            var aliceAgg = result.Single(r => r.CustomerName == "Alice");
            aliceAgg.OrderCount.Should().Be(2);
            aliceAgg.TotalAmount.Should().Be(30m);

            var bobAgg = result.Single(r => r.CustomerName == "Bob");
            bobAgg.OrderCount.Should().Be(1);
            bobAgg.TotalAmount.Should().Be(30m);
        }
    }

    [Fact]
    public void Group_Orders_By_Customer_With_Having_Minimum_OrderCount()
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

            // Alice: 3 orders
            new TestOrder(uow) { Customer = alice, ProductName = "A1", Quantity = 1, Total = 10m };
            new TestOrder(uow) { Customer = alice, ProductName = "A2", Quantity = 2, Total = 20m };
            new TestOrder(uow) { Customer = alice, ProductName = "A3", Quantity = 3, Total = 30m };

            // Bob: 1 order
            new TestOrder(uow) { Customer = bob, ProductName = "B1", Quantity = 1, Total = 5m };

            // Carol: 2 orders
            new TestOrder(uow) { Customer = carol, ProductName = "C1", Quantity = 4, Total = 40m };
            new TestOrder(uow) { Customer = carol, ProductName = "C2", Quantity = 5, Total = 50m };

            uow.CommitChanges();

            int minOrders = 2;

            // -----------------------------
            // The query under test
            // -----------------------------
            var result = uow.Query<TestOrder>()
                .Where(o => o.Customer != null)
                .GroupBy(o => o.Customer!.Name)
                .Where(g => g.Count() >= minOrders) // HAVING g.Count() >= 2
                .Select(g => new
                {
                    CustomerName = g.Key,
                    OrderCount = g.Count(),
                    MinTotal = g.Min(o => o.Total),
                    MaxTotal = g.Max(o => o.Total),
                    AvgTotal = g.Average(o => o.Total)
                })
                .OrderBy(r => r.CustomerName)
                .ToArray();

            // -----------------------------
            // Assertions
            // -----------------------------
            result.Should().HaveCount(2);
            result.Select(r => r.CustomerName)
                  .Should().ContainInOrder("Alice", "Carol");

            var aliceAgg = result.Single(r => r.CustomerName == "Alice");
            aliceAgg.OrderCount.Should().Be(3);
            aliceAgg.MinTotal.Should().Be(10m);
            aliceAgg.MaxTotal.Should().Be(30m);
            aliceAgg.AvgTotal.Should().BeApproximately(20m, 0.001m);

            var carolAgg = result.Single(r => r.CustomerName == "Carol");
            carolAgg.OrderCount.Should().Be(2);
            carolAgg.MinTotal.Should().Be(40m);
            carolAgg.MaxTotal.Should().Be(50m);
            carolAgg.AvgTotal.Should().BeApproximately(45m, 0.001m);
        }
    }

    private static void Cleanup(UnitOfWork uow)
    {
        foreach (var obj in new XPCollection<TestOrder>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<TestCustomer>(uow).ToList()) obj.Delete();
        uow.CommitChanges();
    }
}
