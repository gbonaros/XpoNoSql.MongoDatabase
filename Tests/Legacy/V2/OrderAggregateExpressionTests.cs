using DevExpress.Xpo;

using FluentAssertions;

using System;
using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class OrderAggregateExpressionTests
{
    private readonly DbFixture _fx;
    public OrderAggregateExpressionTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void GroupBy_Customer_Sum_Of_QuantityTimesTotal()
    {
        _fx.Cleanup<TestCustomer>();
        _fx.Cleanup<TestOrder>();
        using (var uow = _fx.NewUow())
        {
            var a = new TestCustomer(uow) { Name = "Alice" };
            var b = new TestCustomer(uow) { Name = "Bob" };

            new TestOrder(uow) { Customer = a, Quantity = 1, Total = 10 };  // 10
            new TestOrder(uow) { Customer = a, Quantity = 2, Total = 20 };  // 40
            new TestOrder(uow) { Customer = b, Quantity = 3, Total = 30 };  // 90
            uow.CommitChanges();

            var result = uow.Query<TestOrder>()
                .Where(o => o.Customer != null)
                .GroupBy(o => o.Customer!.Name)
                .Select(g => new
                {
                    Customer = g.Key,
                    WeightedTotal = g.Sum(o => o.Quantity * o.Total)
                })
                .OrderBy(r => r.Customer)
                .ToArray();

            result.Should().HaveCount(2);

            var alice = result.Single(r => r.Customer == "Alice");
            alice.WeightedTotal.Should().Be(50); // 10 + 40

            var bob = result.Single(r => r.Customer == "Bob");
            bob.WeightedTotal.Should().Be(90);
        }
    }

    [Fact]
    public void Having_On_Average_Quantity()
    {
        _fx.Cleanup<TestCustomer>();
        _fx.Cleanup<TestOrder>();
        using (var uow = _fx.NewUow())
        {
            var a = new TestCustomer(uow) { Name = "Alice" };
            var b = new TestCustomer(uow) { Name = "Bob" };
            var c = new TestCustomer(uow) { Name = "Carol" };

            new TestOrder(uow) { Customer = a, Quantity = 1, Total = 10 };
            new TestOrder(uow) { Customer = a, Quantity = 5, Total = 20 }; // avg 3

            new TestOrder(uow) { Customer = b, Quantity = 1, Total = 30 }; // avg 1

            new TestOrder(uow) { Customer = c, Quantity = 4, Total = 40 };
            new TestOrder(uow) { Customer = c, Quantity = 6, Total = 50 }; // avg 5
            uow.CommitChanges();

            double minAvg = 3.0;

            var result = uow.Query<TestOrder>()
                .Where(o => o.Customer != null)
                .GroupBy(o => o.Customer!.Name)
                .Where(g => g.Average(o => (double)o.Quantity) >= minAvg)
                .Select(g => new
                {
                    Customer = g.Key,
                    AvgQty = g.Average(o => (double)o.Quantity)
                })
                .OrderBy(r => r.Customer)
                .ToArray();

            // With those values: Alice (3) and Carol (5)
            result.Select(r => r.Customer).Should().Equal("Alice", "Carol");
        }
    }
}
