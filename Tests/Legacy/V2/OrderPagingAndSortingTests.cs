
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class OrderPagingAndSortingTests
{
    private readonly DbFixture _fx;
    public OrderPagingAndSortingTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Order_Orders_By_Total_Then_ProductName_With_Skip_Take()
    {
        using (UnitOfWork uow = _fx.NewUow())
        {
            Cleanup(uow);

            // -----------------------------
            // Test data
            // -----------------------------
            var customer = new TestCustomer(uow) { Name = "Customer1" };

            new TestOrder(uow) { Customer = customer, ProductName = "A", Quantity = 1, Total = 5m };
            new TestOrder(uow) { Customer = customer, ProductName = "B", Quantity = 1, Total = 10m };
            new TestOrder(uow) { Customer = customer, ProductName = "C", Quantity = 1, Total = 15m };
            new TestOrder(uow) { Customer = customer, ProductName = "D", Quantity = 1, Total = 20m };
            new TestOrder(uow) { Customer = customer, ProductName = "E", Quantity = 1, Total = 25m };
            uow.CommitChanges();

            // Sort by Total ascending, then ProductName descending, then take a middle page
            var page = uow.Query<TestOrder>()
                .OrderBy(o => o.Total)
                .ThenByDescending(o => o.ProductName)
                .Skip(1)
                .Take(3)
                .Select(o => new { o.ProductName, o.Total })
                .ToArray();

            // -----------------------------
            // Assertions
            // -----------------------------
            page.Should().HaveCount(3);
            page.Select(x => x.Total).Should().BeEquivalentTo<decimal>([10m, 15m, 20m]);
        }
    }

    [Fact]
    public void Get_Top_N_Orders_By_Total_Descending()
    {
        using (UnitOfWork uow = _fx.NewUow())
        {
            Cleanup(uow);

            // -----------------------------
            // Test data
            // -----------------------------
            var customer = new TestCustomer(uow) { Name = "Customer1" };

            new TestOrder(uow) { Customer = customer, ProductName = "Item1", Quantity = 1, Total = 10m };
            new TestOrder(uow) { Customer = customer, ProductName = "Item2", Quantity = 1, Total = 50m };
            new TestOrder(uow) { Customer = customer, ProductName = "Item3", Quantity = 1, Total = 30m };
            new TestOrder(uow) { Customer = customer, ProductName = "Item4", Quantity = 1, Total = 70m };
            new TestOrder(uow) { Customer = customer, ProductName = "Item5", Quantity = 1, Total = 20m };
            uow.CommitChanges();

            int topN = 3;

            var topOrders = uow.Query<TestOrder>()
                .OrderByDescending(o => o.Total)
                .Take(topN)
                .ToArray();

            // -----------------------------
            // Assertions
            // -----------------------------
            topOrders.Should().HaveCount(3);
            topOrders.Select(o => o.Total).Should().ContainInOrder(70m, 50m, 30m);
        }
    }

    private static void Cleanup(UnitOfWork uow)
    {
        foreach (var obj in new XPCollection<TestOrder>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<TestCustomer>(uow).ToList()) obj.Delete();
        uow.CommitChanges();
    }
}
