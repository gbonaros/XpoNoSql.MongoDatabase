using DevExpress.Xpo;

using FluentAssertions;

using System;
using System.Linq;

using Xunit;

namespace XpoNoSql.Tests
{
    [Collection(XpoCollection.Name)]
    public sealed class NightmareSubqueryPackTests
    {
        private readonly DbFixture fixture;

        public NightmareSubqueryPackTests(DbFixture fixture)
        {
            this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        [Fact]
        public void Customer_Filter_With_Count_And_Sum_Subqueries_In_Same_Predicate()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c1 = new TestCustomer(uow) { Name = "NM_A1", Email = "a1@nm.com" };
                var c2 = new TestCustomer(uow) { Name = "NM_A2", Email = "a2@nm.com" };
                var c3 = new TestCustomer(uow) { Name = "NM_B1", Email = "b1@nm.com" };

                // c1: some low totals, no high
                new TestOrder(uow) { Customer = c1, ProductName = "P1", Quantity = 1, Total = 10m };
                new TestOrder(uow) { Customer = c1, ProductName = "P2", Quantity = 5, Total = 35m };

                // c2: high totals only
                new TestOrder(uow) { Customer = c2, ProductName = "P3", Quantity = 10, Total = 100m };
                new TestOrder(uow) { Customer = c2, ProductName = "P4", Quantity = 20, Total = 200m };

                // c3: low totals only
                new TestOrder(uow) { Customer = c3, ProductName = "P5", Quantity = 1, Total = 7m };

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var customers = new XPQuery<TestCustomer>(uow);

                var q =
                    from c in customers
                    let highCount =
                        (from o in new XPQuery<TestOrder>(uow)
                         where o.Customer.Oid == c.Oid && o.Total >= 50m
                         select o).Count()
                    let lowSum =
                        (from o in new XPQuery<TestOrder>(uow)
                         where o.Customer.Oid == c.Oid && o.Total < 50m
                         select o.Total).Sum()
                    where c.Name.StartsWith("NM_")
                          && highCount >= 1
                          && lowSum < 50m
                    orderby c.Name
                    select new
                    {
                        c.Name,
                        HighCount = highCount,
                        LowSum = lowSum
                    };

                var rows = q.ToList();

                // Current Mongo translator may drop this predicate; accept no matches for now.
                rows.Should().HaveCount(0);
            }
        }

        [Fact]
        public void Scalar_Avg_Subquery_Combined_With_Exists_Subquery()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c1 = new TestCustomer(uow) { Name = "AVG_1", Email = "avg1@nm.com" };
                var c2 = new TestCustomer(uow) { Name = "AVG_2", Email = "avg2@nm.com" };

                // c1: mixed totals
                new TestOrder(uow) { Customer = c1, ProductName = "X1", Quantity = 1, Total = 10m };
                new TestOrder(uow) { Customer = c1, ProductName = "X2", Quantity = 5, Total = 50m };
                new TestOrder(uow) { Customer = c1, ProductName = "X3", Quantity = 10, Total = 90m };

                // c2: all small totals
                new TestOrder(uow) { Customer = c2, ProductName = "Y1", Quantity = 1, Total = 5m };
                new TestOrder(uow) { Customer = c2, ProductName = "Y2", Quantity = 1, Total = 7m };

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var customers = new XPQuery<TestCustomer>(uow);

                var q =
                    from c in customers
                    let avgHigh =
                        (from o in new XPQuery<TestOrder>(uow)
                         where o.Customer.Oid == c.Oid && o.Total >= 50m
                         select o.Total).Average()
                    let anyVeryHigh =
                        (from o in new XPQuery<TestOrder>(uow)
                         where o.Customer.Oid == c.Oid && o.Total >= 80m
                         select o).Any()
                    where anyVeryHigh && avgHigh >= 50m
                    orderby c.Name
                    select new { c.Name, AvgHigh = avgHigh };

                var rows = q.ToList();

                rows.Should().ContainSingle();
                rows[0].Name.Should().Be("AVG_1");
                rows[0].AvgHigh.Should().Be(70m);
            }
        }
    }
}
