using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System;
using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests
{
    [Collection(XpoCollection.Name)]
    public sealed class NightmareConditionalSubqueryPackTests
    {
        private readonly DbFixture fixture;

        public NightmareConditionalSubqueryPackTests(DbFixture fixture)
        {
            this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        /// <summary>
        /// LINQ: conditional expression inside a scalar subquery (SUM)
        ///
        /// weightedSum = Sum( Iif(Total >= 50, Total * 2, Total) )
        ///
        /// Data:
        ///   C1: 10, 40   -> 10 + 40 = 50 (no doubling)
        ///   C2: 50, 60   -> 50*2 + 60*2 = 220
        ///
        /// Filter: weightedSum >= 100
        /// Expect: only C2.
        /// </summary>
        [Fact]
        public void Scalar_Subquery_With_Conditional_Weighting()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c1 = new TestCustomer(uow) { Name = "CondSub_C1", Email = "cs1@example.com" };
                var c2 = new TestCustomer(uow) { Name = "CondSub_C2", Email = "cs2@example.com" };

                // C1: 10, 40
                new TestOrder(uow) { Customer = c1, ProductName = "C1_10", Quantity = 1, Total = 10m };
                new TestOrder(uow) { Customer = c1, ProductName = "C1_40", Quantity = 1, Total = 40m };

                // C2: 50, 60
                new TestOrder(uow) { Customer = c2, ProductName = "C2_50", Quantity = 1, Total = 50m };
                new TestOrder(uow) { Customer = c2, ProductName = "C2_60", Quantity = 1, Total = 60m };

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var customers = new XPQuery<TestCustomer>(uow);
                var orders = new XPQuery<TestOrder>(uow);

                var q =
                    from c in customers
                    let weightedSum =
                        (from o in orders
                         where o.Customer.Oid == c.Oid
                         select (o.Total >= 50m ? o.Total * 2m : o.Total))
                        .Sum()
                    where weightedSum >= 100m
                    orderby c.Name
                    select new
                    {
                        c.Name,
                        Weighted = weightedSum
                    };

                var rows = q.ToList();

                rows.Should().HaveCount(1);
                rows[0].Name.Should().Be("CondSub_C2");
                rows[0].Weighted.Should().Be(220m);
            }
        }

        /// <summary>
        /// XPView: Iif used both in projection and ordering expression.
        ///
        /// Score = Iif(Total >= 50, 100, 10)
        ///
        /// Sort by Score desc, then by ProductName asc.
        ///
        /// Data:
        ///   T10  -> Score 10
        ///   T60  -> Score 100
        ///   T55  -> Score 100
        ///   T05  -> Score 10
        ///
        /// Expected order of ProductName:
        ///   T55, T60, T05, T10   (100 group sorted by name asc, then 10 group)
        /// </summary>
        [Fact]
        public void XPView_Conditional_Score_Used_In_OrderBy()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c = new TestCustomer(uow) { Name = "CondOrder_Cust", Email = "co@example.com" };

                new TestOrder(uow) { Customer = c, ProductName = "T10", Quantity = 1, Total = 10m };
                new TestOrder(uow) { Customer = c, ProductName = "T60", Quantity = 1, Total = 60m };
                new TestOrder(uow) { Customer = c, ProductName = "T55", Quantity = 1, Total = 55m };
                new TestOrder(uow) { Customer = c, ProductName = "T05", Quantity = 1, Total = 5m };

                uow.CommitChanges();

                var view = new XPView(uow, typeof(TestOrder));

                // Score expression
                var scoreExpr = "Iif([Total] >= 50, 100, 10)";

                view.Properties.Add(new ViewProperty(
                    "Score",
                    SortDirection.Descending,   // primary sort: Score desc
                    scoreExpr,
                    false,
                    true));

                view.Properties.Add(new ViewProperty(
                    "Name",
                    SortDirection.Ascending,    // secondary sort: ProductName asc
                    "[ProductName]",
                    false,
                    true));

                var rows = view.Cast<ViewRecord>()
                    .Select(r => new
                    {
                        Score = Convert.ToInt32(r["Score"]),
                        Name = (string)r["Name"]
                    })
                    .ToList();

                // Just to be explicit on the ordering:
                var ordered = rows
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Name)
                    .ToList();

                // pipeline should already produce this order, but we assert it explicitly:
                rows.Select(r => r.Name).Should().Equal("T55", "T60", "T05", "T10");
                ordered.Select(r => r.Name).Should().Equal("T55", "T60", "T05", "T10");
            }
        }

        /// <summary>
        /// Criteria: multi-branch Iif (2N+1) used in filter expression.
        ///
        /// Expr:
        ///   Iif(Total < 20, 'L',
        ///       Total < 50, 'M',
        ///       'H') = 'M'
        ///
        /// Data totals: 10(L), 25(M), 45(M), 60(H)
        ///
        /// Expected ProductNames: T25, T45.
        /// </summary>
        [Fact]
        public void Criteria_MultiBranch_Iif_Filter_By_Category()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c = new TestCustomer(uow) { Name = "CondMulti_Cust", Email = "cm@example.com" };

                new TestOrder(uow) { Customer = c, ProductName = "T10", Quantity = 1, Total = 10m }; // L
                new TestOrder(uow) { Customer = c, ProductName = "T25", Quantity = 1, Total = 25m }; // M
                new TestOrder(uow) { Customer = c, ProductName = "T45", Quantity = 1, Total = 45m }; // M
                new TestOrder(uow) { Customer = c, ProductName = "T60", Quantity = 1, Total = 60m }; // H

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var criteria = CriteriaOperator.Parse(
                    "Iif([Total] < 20, 'L', [Total] < 50, 'M', 'H') = 'M'");

                var coll = new XPCollection<TestOrder>(uow, criteria);

                var products = coll
                    .OrderBy(o => o.Total)
                    .Select(o => o.ProductName)
                    .ToList();

                products.Should().Equal("T25", "T45");
            }
        }
    }
}
