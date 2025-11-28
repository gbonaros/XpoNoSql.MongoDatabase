using DevExpress.Xpo;

using FluentAssertions;

using System;
using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests
{
    [Collection(XpoCollection.Name)]
    public sealed class MongoXpViewAbuseTests
    {
        private readonly DbFixture fixture;

        public MongoXpViewAbuseTests(DbFixture f) => fixture = f;

        [Fact]
        public void XPView_Multiple_Aggregates_And_Deep_Keys()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c1 = new TestCustomer(uow) { Name = "KeyA", Email = "e1" };
                var c2 = new TestCustomer(uow) { Name = "KeyB", Email = "e2" };

                new TestOrder(uow) { Customer = c1, ProductName = "A1", Quantity = 5, Total = 10 };
                new TestOrder(uow) { Customer = c1, ProductName = "A2", Quantity = 15, Total = 20 };
                new TestOrder(uow) { Customer = c2, ProductName = "B1", Quantity = 1, Total = 5 };
                new TestOrder(uow) { Customer = c2, ProductName = "B2", Quantity = 100, Total = 50 };

                uow.CommitChanges();

                var view = new XPView(uow, typeof(TestOrder))
                {
                    Properties =
                    {
                        new ViewProperty("Cust", SortDirection.Ascending, "Customer.Name", true, true),
                        new ViewProperty("MinQ", SortDirection.None, "Min([Quantity])", false, true),
                        new ViewProperty("MaxQ", SortDirection.None, "Max([Quantity])", false, true),
                        new ViewProperty("SumT", SortDirection.None, "Sum([Total])", false, true),
                        new ViewProperty("AvgT", SortDirection.None, "Avg([Total])", false, true)
                    }
                };

                var rows = view.Cast<ViewRecord>()
                    .Select(r => new
                    {
                        C = (string)r["Cust"],
                        MinQ = Convert.ToInt32(r["MinQ"]),
                        MaxQ = Convert.ToInt32(r["MaxQ"]),
                        SumT = Convert.ToDecimal(r["SumT"]),
                        AvgT = Convert.ToDecimal(r["AvgT"])
                    })
                    .OrderBy(x => x.C)
                    .ToList();

                rows.Should().HaveCount(2);

                var a = rows[0];
                a.C.Should().Be("KeyA");
                a.MinQ.Should().Be(5);
                a.MaxQ.Should().Be(15);
                a.SumT.Should().Be(30m);
                a.AvgT.Should().Be(15m);

                var b = rows[1];
                b.C.Should().Be("KeyB");
                b.MinQ.Should().Be(1);
                b.MaxQ.Should().Be(100);
                b.SumT.Should().Be(55m);
                b.AvgT.Should().Be(27.5m);
            }
        }
    }
}
