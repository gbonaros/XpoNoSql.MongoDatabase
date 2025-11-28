
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class JoinAggregateHavingSortTests
{
    private readonly DbFixture _fx;
    public JoinAggregateHavingSortTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void GroupBy_Customer_With_OrderStats_And_Sort_By_Aggregates()
    {
        using (var uow = _fx.NewUow())
        {
            Cleanup(uow);

            var a = new TestCustomer(uow) { Name = "Alice" };
            var b = new TestCustomer(uow) { Name = "Bob" };
            var c = new TestCustomer(uow) { Name = "Carl" };

            new TestOrder(uow) { Customer = a, Quantity = 1, Total = 20 };
            new TestOrder(uow) { Customer = a, Quantity = 2, Total = 40 };
            new TestOrder(uow) { Customer = b, Quantity = 1, Total = 5 };
            new TestOrder(uow) { Customer = c, Quantity = 10, Total = 100 };
            new TestOrder(uow) { Customer = c, Quantity = 5, Total = 20 };
            uow.CommitChanges();

            var result =
                uow.Query<TestOrder>()
                .Where(o => o.Customer != null)
                .GroupBy(o => o.Customer.Name)
                .Where(g => g.Sum(x => x.Total) >= 30)   // HAVING
                .Select(g => new
                {
                    Customer = g.Key,
                    Count = g.Count(),
                    Total = g.Sum(x => x.Total),
                    MaxQty = g.Max(x => x.Quantity)
                })
                .OrderByDescending(x => x.Total)
                .ThenBy(x => x.Customer)
                .ToArray();

            /*
                Expected groups:
                    Alice -> Total 60
                    Carl  -> Total 120
                    Bob excluded
            */

            result.Should().HaveCount(2);
            result[0].Customer.Should().Be("Carl");
            result[1].Customer.Should().Be("Alice");
        }
    }

    private static void Cleanup(UnitOfWork uow)
    {
        foreach (var o in new XPCollection<TestOrder>(uow).ToList()) o.Delete();
        foreach (var c in new XPCollection<TestCustomer>(uow).ToList()) c.Delete();
        uow.CommitChanges();
    }
}
