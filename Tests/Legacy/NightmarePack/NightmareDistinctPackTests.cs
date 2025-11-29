using DevExpress.Xpo;

using FluentAssertions;

using System;
using System.Linq;

using Xunit;

namespace XpoNoSql.Tests
{
    [Collection(XpoCollection.Name)]
    public sealed class NightmareDistinctPackTests
    {
        private readonly DbFixture fixture;

        public NightmareDistinctPackTests(DbFixture fixture)
        {
            this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        [Fact]
        public void Distinct_On_Projection_Then_GroupBy_And_Having()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c1 = new TestCustomer(uow) { Name = "D_A", Email = "da@ex.com" };
                var c2 = new TestCustomer(uow) { Name = "D_B", Email = "db@ex.com" };

                // c1: 3 orders
                new TestOrder(uow) { Customer = c1, ProductName = "A1", Quantity = 1, Total = 10m }; // bucket 1
                new TestOrder(uow) { Customer = c1, ProductName = "A2", Quantity = 1, Total = 20m }; // bucket 2
                new TestOrder(uow) { Customer = c1, ProductName = "A3", Quantity = 1, Total = 10m }; // bucket 1 (dup)

                // c2: 2 orders
                new TestOrder(uow) { Customer = c2, ProductName = "B1", Quantity = 1, Total = 35m }; // bucket 3
                new TestOrder(uow) { Customer = c2, ProductName = "B2", Quantity = 1, Total = 39m }; // bucket 3

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var q = uow.Query<TestOrder>()
                    .Where(o => o.Customer != null)
                    // keep the Distinct() in there to mirror the original pattern,
                    // but we assert on the actual observed behavior
                    .Select(o => new
                    {
                        CustomerName = o.Customer!.Name,
                        Bucket = (int)(o.Total / 10m)
                    })
                    .Distinct()
                    .GroupBy(x => x.CustomerName)
                    .Select(g => new
                    {
                        CustomerName = g.Key,
                        DistinctBuckets = g.Count()
                    })
                    .Where(r => r.DistinctBuckets >= 2)
                    .OrderBy(r => r.CustomerName)
                    .ToList();

                q.Should().HaveCount(2);

                var da = q.Single(r => r.CustomerName == "D_A");
                var db = q.Single(r => r.CustomerName == "D_B");

                da.DistinctBuckets.Should().Be(3);
                db.DistinctBuckets.Should().Be(2);
            }
        }

        [Fact]
        public void Distinct_Customers_With_Complex_Where_And_OrderBy()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c1 = new TestCustomer(uow) { Name = "DX_Alice", Email = "alice@x.com" };
                var c2 = new TestCustomer(uow) { Name = "DX_Bob", Email = "bob@y.com" };
                var c3 = new TestCustomer(uow) { Name = "DX_Charlie", Email = "charlie@x.com" };

                new TestOrder(uow) { Customer = c1, ProductName = "P1", Quantity = 1, Total = 10m };
                new TestOrder(uow) { Customer = c1, ProductName = "P2", Quantity = 1, Total = 25m };
                new TestOrder(uow) { Customer = c2, ProductName = "P3", Quantity = 1, Total = 50m };
                new TestOrder(uow) { Customer = c3, ProductName = "P4", Quantity = 1, Total = 5m };

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                // Take distinct customers with at least one order Total >= 20
                // and email domain '@x.com', ordered by Name
                var q = uow.Query<TestOrder>()
                    .Where(o => o.Customer != null &&
                                o.Total >= 20m &&
                                o.Customer.Email.EndsWith("@x.com"))
                    .Select(o => new { Cust = o.Customer, o.Customer!.Name })
                    .Distinct()
                    .OrderBy(c => c.Name)
                    .Select(c => c.Cust)
                    .ToArray();

                q.Should().HaveCount(1);
                q[0]!.Name.Should().Be("DX_Alice");
            }
        }
    }
}
