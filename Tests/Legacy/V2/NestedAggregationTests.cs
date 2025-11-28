
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class NestedAggregationTests
{
    private readonly DbFixture _fx;
    public NestedAggregationTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void GroupBy_Customer_And_TotalBucket_Having_Count_GreaterOrEqual_2()
    {
        using (var uow = _fx.NewUow())
        {
            Cleanup(uow);

            var c1 = new TestCustomer(uow) { Name = "A" };
            var c2 = new TestCustomer(uow) { Name = "B" };

            // A: 10.1, 10.9, 20.0
            new TestOrder(uow) { Customer = c1, Total = 10.1m };
            new TestOrder(uow) { Customer = c1, Total = 10.9m };
            new TestOrder(uow) { Customer = c1, Total = 20.0m };

            // B: 10.0, 10.0
            new TestOrder(uow) { Customer = c2, Total = 10.0m };
            new TestOrder(uow) { Customer = c2, Total = 10.0m };

            uow.CommitChanges();

            // Single-level grouping:
            // GROUP BY (CustomerName, Bucket = Total / 10)
            // HAVING COUNT(*) >= 2
            var query =
                uow.Query<TestOrder>()
                    .Where(o => o.Customer != null)
                    .GroupBy(o => new
                    {
                        CustomerName = o.Customer!.Name,
                        // "Bucket" concept: 0–9.99 => 0, 10–19.99 => 1, etc.
                        TotalBucket = (int)(o.Total / 10m)
                    })
                    .Where(g => g.Count() >= 2)
                    .Select(g => new
                    {
                        g.Key.CustomerName,
                        g.Key.TotalBucket,
                        Count = g.Count(),
                        AvgTotal = g.Average(o => o.Total)
                    })
                    .OrderBy(r => r.CustomerName)
                    .ThenBy(r => r.TotalBucket)
                    .ToArray();

            // Expected groups:
            // A, bucket 1: 10.1 + 10.9  -> count=2
            // B, bucket 1: 10.0 + 10.0  -> count=2
            query.Should().HaveCount(2);

            var a = query.Single(x => x.CustomerName == "A" && x.TotalBucket == 1);
            a.Count.Should().Be(2);

            var b = query.Single(x => x.CustomerName == "B" && x.TotalBucket == 1);
            b.Count.Should().Be(2);
        }
    }

    private static void Cleanup(UnitOfWork uow)
    {
        foreach (var o in new XPCollection<TestOrder>(uow).ToList()) o.Delete();
        foreach (var c in new XPCollection<TestCustomer>(uow).ToList()) c.Delete();
        uow.CommitChanges();
    }
}
