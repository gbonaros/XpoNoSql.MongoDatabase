using DevExpress.Xpo;

using FluentAssertions;

using System;
using System.Linq;

using Xunit;

namespace XpoNoSql.Tests
{
    [Collection(XpoCollection.Name)]
    public sealed class NightmareMultiJoinPackTests
    {
        private readonly DbFixture fixture;

        public NightmareMultiJoinPackTests(DbFixture fixture)
        {
            this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        /// <summary>
        /// Orders (o1) -> Customer (c) -> Orders (o2) multi-join.
        ///
        /// We create:
        ///   Customer A: orders 10, 30
        ///   Customer B: orders  5, 50
        ///
        /// Query:
        ///  from o1 in Orders
        ///  join c  in Customers on o1.Customer.Oid equals c.Oid
        ///  join o2 in Orders    on c.Oid equals o2.Customer.Oid
        ///  where o1.Total < o2.Total        // strict pair ordering
        ///  orderby c.Name, o1.Total, o2.Total
        ///  select (CustomerName, LeftTotal, RightTotal)
        ///
        /// Expected pairs:
        ///   A: (10,30)
        ///   B: (5,50)
        /// So exactly 2 rows, one per customer.
        /// </summary>
        [Fact]
        public void ThreeWay_Orders_Customers_Orders_MultiJoin_Pairs_By_Total()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var a = new TestCustomer(uow) { Name = "Multi_A", Email = "a@multi.com" };
                var b = new TestCustomer(uow) { Name = "Multi_B", Email = "b@multi.com" };

                // Customer A: 10, 30
                new TestOrder(uow) { Customer = a, ProductName = "A1", Quantity = 1, Total = 10m };
                new TestOrder(uow) { Customer = a, ProductName = "A2", Quantity = 1, Total = 30m };

                // Customer B: 5, 50
                new TestOrder(uow) { Customer = b, ProductName = "B1", Quantity = 1, Total = 5m };
                new TestOrder(uow) { Customer = b, ProductName = "B2", Quantity = 1, Total = 50m };

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var orders = new XPQuery<TestOrder>(uow);
                var customers = new XPQuery<TestCustomer>(uow);

                var q =
                    from o1 in orders
                    join c in customers on o1.Customer.Oid equals c.Oid
                    join o2 in orders on c.Oid equals o2.Customer.Oid
                    where o1.Total < o2.Total
                    orderby c.Name, o1.Total, o2.Total
                    select new
                    {
                        CustomerName = c.Name,
                        LeftTotal = o1.Total,
                        RightTotal = o2.Total
                    };

                var rows = q.ToList();

                rows.Should().HaveCount(2);

                rows[0].CustomerName.Should().Be("Multi_A");
                rows[0].LeftTotal.Should().Be(10m);
                rows[0].RightTotal.Should().Be(30m);

                rows[1].CustomerName.Should().Be("Multi_B");
                rows[1].LeftTotal.Should().Be(5m);
                rows[1].RightTotal.Should().Be(50m);
            }
        }

        /// <summary>
        /// Multi-join + group + having:
        ///
        /// Data:
        ///   C1: orders  5, 15, 25
        ///   C2: orders 50, 60
        ///   C3: order  3
        ///
        /// Query:
        ///   from o in Orders
        ///   join c in Customers on o.Customer.Oid equals c.Oid
        ///   where o.Total >= 5
        ///   group o by c.Name into g
        ///   where g.Average(x => x.Total) >= 20 && g.Count() >= 2
        ///   orderby g.Key
        ///   select (CustomerName, Count, AvgTotal)
        ///
        /// Expected:
        ///   C1 -> (3, (5+15+25)/3 = 15)  => filtered out (Avg < 20)
        ///   C2 -> (2, (50+60)/2 = 55)    => kept
        ///   C3 -> (0 or 1 but Total < 5) => filtered at where stage
        ///
        /// So only "G_C2" survives.
        /// </summary>
        [Fact]
        public void MultiJoin_GroupBy_Customer_With_Complex_Having()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c1 = new TestCustomer(uow) { Name = "G_C1", Email = "c1@multi.com" };
                var c2 = new TestCustomer(uow) { Name = "G_C2", Email = "c2@multi.com" };
                var c3 = new TestCustomer(uow) { Name = "G_C3", Email = "c3@multi.com" };

                // C1: 5, 15, 25
                new TestOrder(uow) { Customer = c1, ProductName = "C1_1", Quantity = 1, Total = 5m };
                new TestOrder(uow) { Customer = c1, ProductName = "C1_2", Quantity = 1, Total = 15m };
                new TestOrder(uow) { Customer = c1, ProductName = "C1_3", Quantity = 1, Total = 25m };

                // C2: 50, 60
                new TestOrder(uow) { Customer = c2, ProductName = "C2_1", Quantity = 1, Total = 50m };
                new TestOrder(uow) { Customer = c2, ProductName = "C2_2", Quantity = 1, Total = 60m };

                // C3: 3 (won’t pass Total >= 5 filter)
                new TestOrder(uow) { Customer = c3, ProductName = "C3_1", Quantity = 1, Total = 3m };

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var orders = new XPQuery<TestOrder>(uow);
                var customers = new XPQuery<TestCustomer>(uow);

                var q =
                    from o in orders
                    join c in customers on o.Customer.Oid equals c.Oid
                    where o.Total >= 5m
                    group o by c.Name into g
                    where g.Average(x => x.Total) >= 20m && g.Count() >= 2
                    orderby g.Key
                    select new
                    {
                        CustomerName = g.Key,
                        Count = g.Count(),
                        AvgTotal = g.Average(x => x.Total)
                    };

                var rows = q.ToList();

                rows.Should().HaveCount(1);
                rows[0].CustomerName.Should().Be("G_C2");
                rows[0].Count.Should().Be(2);
                rows[0].AvgTotal.Should().Be(55m);
            }
        }
    }
}
