
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;



using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class OrderTopPerCustomerTests
{
    private readonly DbFixture _fx;
    public OrderTopPerCustomerTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Max_Order_Total_Per_Customer()
    {
        using (var uow = _fx.NewUow())
        {
            Cleanup(uow);

            var a = new TestCustomer(uow) { Name = "Alice" };
            var b = new TestCustomer(uow) { Name = "Bob" };

            new TestOrder(uow) { Customer = a, ProductName = "A1", Total = 10 };
            new TestOrder(uow) { Customer = a, ProductName = "A2", Total = 50 };
            new TestOrder(uow) { Customer = a, ProductName = "A3", Total = 20 };

            new TestOrder(uow) { Customer = b, ProductName = "B1", Total = 5 };
            new TestOrder(uow) { Customer = b, ProductName = "B2", Total = 15 };
            uow.CommitChanges();

            var result = uow.Query<TestOrder>()
                .Where(o => o.Customer != null)
                .GroupBy(o => o.Customer!.Name)
                .Select(g => new
                {
                    Customer = g.Key,
                    MaxTotal = g.Max(o => o.Total),
                    OrderCount = g.Count()
                })
                .OrderBy(r => r.Customer)
                .ToArray();

            result.Should().HaveCount(2);

            var alice = result[0];
            alice.Customer.Should().Be("Alice");
            alice.MaxTotal.Should().Be(50);
            alice.OrderCount.Should().Be(3);

            var bob = result[1];
            bob.Customer.Should().Be("Bob");
            bob.MaxTotal.Should().Be(15);
            bob.OrderCount.Should().Be(2);
        }
    }

    [Fact]
    public void Customers_With_Max_Order_Above_Threshold()
    {
        using (var uow = _fx.NewUow())
        {
            Cleanup(uow);

            var a = new TestCustomer(uow) { Name = "Alice" };
            var b = new TestCustomer(uow) { Name = "Bob" };
            var c = new TestCustomer(uow) { Name = "Carol" };

            new TestOrder(uow) { Customer = a, Total = 10 };
            new TestOrder(uow) { Customer = a, Total = 20 };

            new TestOrder(uow) { Customer = b, Total = 5 };

            new TestOrder(uow) { Customer = c, Total = 100 };
            uow.CommitChanges();

            decimal threshold = 25m;

            var result = uow.Query<TestOrder>()
                .Where(o => o.Customer != null)
                .GroupBy(o => o.Customer!.Name)
                .Where(g => g.Max(o => o.Total) >= threshold)
                .Select(g => new
                {
                    Customer = g.Key,
                    MaxTotal = g.Max(o => o.Total)
                })
                .OrderBy(r => r.Customer)
                .ToArray();

            result.Select(r => r.Customer).Should().Equal("Carol");
        }
    }

    private static void Cleanup(UnitOfWork uow)
    {
        foreach (var o in new XPCollection<TestOrder>(uow).ToList()) o.Delete();
        foreach (var c in new XPCollection<TestCustomer>(uow).ToList()) c.Delete();
        uow.CommitChanges();
    }
}
