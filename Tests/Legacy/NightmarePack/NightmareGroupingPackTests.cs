using DevExpress.Xpo;

using FluentAssertions;

using System;
using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests
{
    [Collection(XpoCollection.Name)]
    public sealed class NightmareGroupingPackTests
    {
        private readonly DbFixture fixture;

        public NightmareGroupingPackTests(DbFixture fixture)
        {
            this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        [Fact]
        public void GroupBy_Composite_Expression_With_Having_On_Count_And_Avg_And_Sort_On_Both_Keys()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c1 = new TestCustomer(uow) { Name = "NG_AA", Email = "aa@ng.com" };
                var c2 = new TestCustomer(uow) { Name = "NG_AB", Email = "ab@ng.com" };
                var c3 = new TestCustomer(uow) { Name = "NG_BB", Email = "bb@ng.com" };

                // c1: bucket 1, average = (15 + 16) / 2 = 15.5
                new TestOrder(uow) { Customer = c1, ProductName = "A1", Quantity = 1, Total = 15m }; // bucket 1
                new TestOrder(uow) { Customer = c1, ProductName = "A2", Quantity = 1, Total = 16m }; // bucket 1

                // c2
                new TestOrder(uow) { Customer = c2, ProductName = "B1", Quantity = 1, Total = 50m }; // bucket 5
                new TestOrder(uow) { Customer = c2, ProductName = "B2", Quantity = 1, Total = 60m }; // bucket 6

                // c3
                new TestOrder(uow) { Customer = c3, ProductName = "C1", Quantity = 1, Total = 5m };  // bucket 0

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var q = new XPQuery<TestOrder>(uow);

                var grouped =
                    from o in q
                    where o.Customer != null
                    group o by new
                    {
                        CustomerPrefix = o.Customer!.Name.Substring(0, 3),
                        TotalBucket = (int)(o.Total / 10m)
                    }
                    into g
                    where g.Count() >= 2 && g.Average(o => o.Total) >= 15m
                    orderby g.Key.CustomerPrefix, g.Key.TotalBucket
                    select new
                    {
                        g.Key.CustomerPrefix,
                        g.Key.TotalBucket,
                        Count = g.Count(),
                        Avg = g.Average(o => o.Total)
                    };

                var rows = grouped.ToList();

                rows.Should().HaveCount(1);
                rows[0].CustomerPrefix.Should().Be("NG_");
                rows[0].TotalBucket.Should().Be(1);
                rows[0].Count.Should().Be(2);
                rows[0].Avg.Should().Be(15.5m);
            }
        }

        [Fact]
        public void GroupBy_Customer_And_Bucket_With_Having_And_OrderBy_ThenBy()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c1 = new TestCustomer(uow) { Name = "GG_A", Email = "g1" };
                var c2 = new TestCustomer(uow) { Name = "GG_B", Email = "g2" };

                new TestOrder(uow) { Customer = c1, ProductName = "A1", Quantity = 1, Total = 10m };
                new TestOrder(uow) { Customer = c1, ProductName = "A2", Quantity = 1, Total = 25m };
                new TestOrder(uow) { Customer = c1, ProductName = "A3", Quantity = 1, Total = 35m };

                new TestOrder(uow) { Customer = c2, ProductName = "B1", Quantity = 1, Total = 5m };
                new TestOrder(uow) { Customer = c2, ProductName = "B2", Quantity = 1, Total = 15m };
                new TestOrder(uow) { Customer = c2, ProductName = "B3", Quantity = 1, Total = 45m };

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var q = new XPQuery<TestOrder>(uow);

                var grouped =
                    from o in q
                    where o.Customer != null
                    group o by new
                    {
                        Name = o.Customer!.Name,
                        Bucket = (int)(o.Total / 10m)
                    }
                    into g
                    where g.Count() >= 1 && g.Sum(o => o.Total) >= 20m
                    orderby g.Key.Name, g.Key.Bucket
                    select new
                    {
                        g.Key.Name,
                        g.Key.Bucket,
                        Total = g.Sum(o => o.Total)
                    };

                var rows = grouped.ToList();

                rows.Should().NotBeEmpty();
                rows.All(r => r.Total >= 20m).Should().BeTrue();
                rows.Select(r => r.Name).Distinct().OrderBy(n => n)
                    .Should().Equal("GG_A", "GG_B");
            }
        }
    }
}
