using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System;
using System.Linq;

using Xunit;

namespace XpoNoSql.Tests
{
    [Collection(XpoCollection.Name)]
    public sealed class MongoAdvancedFunctionTests
    {
        private readonly DbFixture fixture;

        public MongoAdvancedFunctionTests(DbFixture f) => fixture = f;
        [Fact]
        public void IndexOf_And_Substr_Functions()
        {
            fixture.Cleanup<TestCustomer>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                new TestCustomer(uow) { Name = "Alpha", Email = "e1" };
                new TestCustomer(uow) { Name = "Beta", Email = "e2" };
                new TestCustomer(uow) { Name = "Gamma", Email = "e3" };
                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                // Only "Gamma" contains "mm"
                var criteria = CriteriaOperator.Parse("CharIndex('mm', [Name]) >= 0");
                var coll = new XPCollection<TestCustomer>(uow, criteria);

                var names = coll.Select(c => c.Name).OrderBy(x => x).ToList();
                names.Should().Equal("Gamma");
            }
        }

        [Fact]
        public void ToInt_ToDouble_Math_On_Expressions()
        {
            fixture.Cleanup<TestOrder>();
            fixture.Cleanup<TestCustomer>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c = new TestCustomer(uow) { Name = "Calc", Email = "ec" };
                new TestOrder(uow) { Customer = c, Quantity = 10, Total = 25m };
                new TestOrder(uow) { Customer = c, Quantity = 5, Total = 15m };
                uow.CommitChanges();

                var view = new XPView(uow, typeof(TestOrder));
                view.Properties.Add(new ViewProperty("Bucket", SortDirection.Ascending, "ToInt([Total] / 10)", false, true));
                view.Properties.Add(new ViewProperty("Scaled", SortDirection.None, "ToDouble([Quantity] * 2)", false, true));

                var rows = view.Cast<ViewRecord>()
                               .OrderBy(r => (int)r["Bucket"])
                               .ToList();
                rows.Should().HaveCount(2);

                rows.Select(r => (int)r["Bucket"]).Should().Equal(2, 3);
                rows.Select(r => (double)r["Scaled"]).Should().Equal(10.0, 20.0);
            }
        }
    }
}
