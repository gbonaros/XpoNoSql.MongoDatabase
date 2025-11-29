using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System;
using System.Linq;

using Xunit;

namespace XpoNoSql.Tests
{
    [Collection(XpoCollection.Name)]
    public sealed class NightmareFunctionPackTests
    {
        private readonly DbFixture fixture;

        public NightmareFunctionPackTests(DbFixture fixture)
        {
            this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        [Fact]
        public void Complex_Criteria_With_Concat_Trim_Len_Contains_CharIndex()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c1 = new TestCustomer(uow) { Name = " FN_A ", Email = "fn_a@example.com" };
                var c2 = new TestCustomer(uow) { Name = "fn_b", Email = "fn_b@example.com" };
                var c3 = new TestCustomer(uow) { Name = "X", Email = "x@example.com" };

                new TestOrder(uow) { Customer = c1, ProductName = "ax_123", Quantity = 1, Total = 10m };
                new TestOrder(uow) { Customer = c2, ProductName = "BX_999", Quantity = 10, Total = 20m };
                new TestOrder(uow) { Customer = c3, ProductName = "zz", Quantity = 5, Total = 30m };

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                // Conditions:
                // - Trimmed Name starts with 'FN_'
                // - Len(ProductName) >= 5
                // - CharIndex('x', ProductName) >= 0  (case-insensitive)
                // - Email contains 'example'
                var criteria = CriteriaOperator.Parse(
                    "StartsWith(Trim([Customer.Name]), 'FN_') " +
                    "AND Len([ProductName]) >= 5 " +
                    "AND CharIndex('x', [ProductName]) >= 0 " +
                    "AND Contains([Customer.Email], 'example')");

                var coll = new XPCollection<TestOrder>(uow, criteria);
                var rows = coll
                    .Select(o => new
                    {
                        NameTrimmed = o.Customer!.Name.Trim(),
                        o.ProductName,
                        o.Customer.Email
                    })
                    .OrderBy(x => x.NameTrimmed)
                    .ToList();

                rows.Should().HaveCount(2);
                rows.Select(r => r.NameTrimmed).Should().Equal("FN_A", "fn_b");
                rows.Select(r => r.ProductName).Should().BeEquivalentTo(new[] { "ax_123", "BX_999" });
            }
        }

        [Fact]
        public void XPView_With_Numeric_Conversions_And_Sorting_On_Expression()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c = new TestCustomer(uow) { Name = "Conv", Email = "conv@nm.com" };

                new TestOrder(uow) { Customer = c, ProductName = "C1", Quantity = 3, Total = 12m };
                new TestOrder(uow) { Customer = c, ProductName = "C2", Quantity = 10, Total = 45m };
                new TestOrder(uow) { Customer = c, ProductName = "C3", Quantity = 5, Total = 27m };

                uow.CommitChanges();

                var view = new XPView(uow, typeof(TestOrder))
                {
                    Criteria = CriteriaOperator.Parse("Customer.Name = 'Conv'")
                };

                view.Properties.Add(new ViewProperty("Bucket", SortDirection.Ascending,
                    "ToInt([Total] / 10)", false, true));
                view.Properties.Add(new ViewProperty("QDouble", SortDirection.None,
                    "ToDouble([Quantity])", false, true));
                view.Properties.Add(new ViewProperty("Name", SortDirection.None,
                    "[ProductName]", false, true));

                var rows = view.Cast<ViewRecord>()
                    .Select(r => new
                    {
                        Bucket = (int)r["Bucket"],
                        QDouble = (double)r["QDouble"],
                        Name = (string)r["Name"]
                    })
                    .OrderBy(r => r.Bucket)
                    .ThenBy(r => r.Name)
                    .ToList();

                rows.Should().HaveCount(3);
                // Ordering by Bucket asc then Name asc
                rows.Select(r => r.Bucket).Should().Equal(1, 3, 5);
            }
        }
    }
}
