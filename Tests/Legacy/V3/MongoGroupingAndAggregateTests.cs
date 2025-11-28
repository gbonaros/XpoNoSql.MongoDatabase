using DevExpress.Xpo;

using FluentAssertions;

using System;
using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests
{
    [Collection(XpoCollection.Name)]
    public sealed class MongoGroupingAndAggregateTests
    {
        private readonly DbFixture fixture;

        public MongoGroupingAndAggregateTests(DbFixture fixture)
        {
            this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        [Fact]
        public void GroupBy_Customer_With_Sum_And_Count()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();
            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c1 = new TestCustomer(uow) { Name = "G-Alice", Email = "ga@example.com" };
                var c2 = new TestCustomer(uow) { Name = "G-Bob", Email = "gb@example.com" };

                new TestOrder(uow) { Customer = c1, ProductName = "P1", Quantity = 1, Total = 10m };
                new TestOrder(uow) { Customer = c1, ProductName = "P2", Quantity = 2, Total = 20m };
                new TestOrder(uow) { Customer = c2, ProductName = "P3", Quantity = 3, Total = 30m };
                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var q = new XPQuery<TestOrder>(uow);

                var results = q
                    .Where(o => o.Customer != null)
                    .GroupBy(o => o.Customer!.Name)
                    .Select(g => new
                    {
                        CustomerName = g.Key,
                        Count = g.Count(),
                        Sum = g.Sum(o => o.Total),
                        Avg = g.Average(o => o.Total)
                    })
                    .OrderBy(r => r.CustomerName)
                    .ToList();

                results.Should().HaveCount(2);

                var alice = results.Single(r => r.CustomerName == "G-Alice");
                alice.Count.Should().Be(2);
                alice.Sum.Should().Be(30m);
                alice.Avg.Should().Be(15m);

                var bob = results.Single(r => r.CustomerName == "G-Bob");
                bob.Count.Should().Be(1);
                bob.Sum.Should().Be(30m);
                bob.Avg.Should().Be(30m);
            }
        }

        [Fact]
        public void GroupBy_Customer_With_Having_On_Count()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();
            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c1 = new TestCustomer(uow) { Name = "H-Alice", Email = "ha@example.com" };
                var c2 = new TestCustomer(uow) { Name = "H-Bob", Email = "hb@example.com" };

                new TestOrder(uow) { Customer = c1, ProductName = "P1", Quantity = 1, Total = 10m };
                new TestOrder(uow) { Customer = c1, ProductName = "P2", Quantity = 2, Total = 20m };
                new TestOrder(uow) { Customer = c2, ProductName = "P3", Quantity = 3, Total = 30m };
                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var q = new XPQuery<TestOrder>(uow);

                var results = q
                    .Where(o => o.Customer != null)
                    .GroupBy(o => o.Customer!.Name)
                    .Where(g => g.Count() >= 2)
                    .Select(g => new
                    {
                        g.Key,
                        Cnt = g.Count()
                    })
                    .OrderBy(r => r.Key)
                    .ToList();

                results.Should().HaveCount(1);
                results[0].Key.Should().Be("H-Alice");
                results[0].Cnt.Should().Be(2);
            }
        }
    }
}
