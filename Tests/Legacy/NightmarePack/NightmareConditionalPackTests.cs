using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System;
using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests
{
    [Collection(XpoCollection.Name)]
    public sealed class NightmareConditionalPackTests
    {
        private readonly DbFixture fixture;

        public NightmareConditionalPackTests(DbFixture fixture)
        {
            this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        /// <summary>
        /// XPView with an Iif() expression used as a group key.
        ///
        /// Data:
        ///   Totals: 10, 20, 60, 80
        ///   "Big"  = Total >= 50
        ///   "Small"= Total <  50
        ///
        /// XPView:
        ///   Key  = Iif([Total] >= 50, 'Big', 'Small')
        ///   Cnt  = Count()
        ///
        /// Expect:
        ///   Big   -> 2
        ///   Small -> 2
        /// </summary>
        [Fact]
        public void XPView_GroupBy_Iif_Bucket_Big_Small()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c = new TestCustomer(uow) { Name = "Cond_Cust", Email = "cond@example.com" };

                new TestOrder(uow) { Customer = c, ProductName = "T10", Quantity = 1, Total = 10m };
                new TestOrder(uow) { Customer = c, ProductName = "T20", Quantity = 1, Total = 20m };
                new TestOrder(uow) { Customer = c, ProductName = "T60", Quantity = 1, Total = 60m };
                new TestOrder(uow) { Customer = c, ProductName = "T80", Quantity = 1, Total = 80m };

                uow.CommitChanges();

                var view = new XPView(uow, typeof(TestOrder))
                {
                    Properties =
                    {
                        // Group key: Big / Small
                        new ViewProperty(
                            "Bucket",
                            SortDirection.Ascending,
                            "Iif([Total] >= 50, 'Big', 'Small')",
                            true,   // is key
                            true),  // show
                        // Aggregate: Count
                        new ViewProperty(
                            "Cnt",
                            SortDirection.None,
                            "Count()",
                            false,
                            true)
                    }
                };

                var rows = view.Cast<ViewRecord>()
                    .Select(r => new
                    {
                        Bucket = (string)r["Bucket"],
                        Cnt = Convert.ToInt32(r["Cnt"])
                    })
                    .OrderBy(x => x.Bucket)
                    .ToList();

                rows.Should().HaveCount(2);

                var big = rows.Single(x => x.Bucket == "Big");
                var small = rows.Single(x => x.Bucket == "Small");

                big.Cnt.Should().Be(2);
                small.Cnt.Should().Be(2);
            }
        }

        /// <summary>
        /// Criteria with Iif inside a numeric expression.
        ///
        /// Expr:
        ///   Iif([Total] >= 20, [Total], 0) >= 40
        ///
        /// Data totals: 10, 20, 40, 50
        /// Row-by-row:
        ///   10 -> Iif(false, 10, 0) = 0   < 40 => exclude
        ///   20 -> Iif(true , 20, 0) = 20  < 40 => exclude
        ///   40 -> Iif(true , 40, 0) = 40 >= 40 => include
        ///   50 -> Iif(true , 50, 0) = 50 >= 40 => include
        /// </summary>
        [Fact]
        public void Criteria_Iif_Inside_Numeric_Expression_Filter()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c = new TestCustomer(uow) { Name = "Cond_Cust2", Email = "cond2@example.com" };

                new TestOrder(uow) { Customer = c, ProductName = "T10", Quantity = 1, Total = 10m };
                new TestOrder(uow) { Customer = c, ProductName = "T20", Quantity = 1, Total = 20m };
                new TestOrder(uow) { Customer = c, ProductName = "T40", Quantity = 1, Total = 40m };
                new TestOrder(uow) { Customer = c, ProductName = "T50", Quantity = 1, Total = 50m };

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var criteria = CriteriaOperator.Parse(
                    "Iif([Total] >= 20, [Total], 0) >= 40");

                var coll = new XPCollection<TestOrder>(uow, criteria);

                var rows = coll
                    .OrderBy(o => o.Total)
                    .Select(o => new { o.ProductName, o.Total })
                    .ToList();

                rows.Should().HaveCount(2);
                rows[0].ProductName.Should().Be("T40");
                rows[1].ProductName.Should().Be("T50");
            }
        }
    }
}
