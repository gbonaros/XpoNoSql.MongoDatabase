using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System;
using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests
{
    [Collection(XpoCollection.Name)]
    public sealed class MongoJoinGraphStressTests
    {
        private readonly DbFixture fixture;

        public MongoJoinGraphStressTests(DbFixture f) => fixture = f;

        [Fact]
        public void MultiHop_Join_Navigation_Filter_Order()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c1 = new TestCustomer(uow) { Name = "A", Email = "A@e.com" };
                var c2 = new TestCustomer(uow) { Name = "B", Email = "B@e.com" };

                new TestOrder(uow) { Customer = c1, ProductName = "X", Total = 9m, Quantity = 1 };
                new TestOrder(uow) { Customer = c1, ProductName = "Y", Total = 99m, Quantity = 100 };
                new TestOrder(uow) { Customer = c2, ProductName = "Z", Total = 5m, Quantity = 10 };

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var q =
                    from o in new XPQuery<TestOrder>(uow)
                    where o.Customer.Email.Contains("@e.com") &&
                          o.Customer.Name == "A" &&
                          o.Quantity >= 10
                    orderby o.Total descending
                    select new
                    {
                        C = o.Customer.Name,
                        o.ProductName,
                        o.Quantity,
                        o.Total
                    };

                var rows = q.ToList();
                rows.Should().HaveCount(1);
                rows[0].ProductName.Should().Be("Y");
                rows[0].Quantity.Should().Be(100);
            }
        }

        [Fact]
        public void Inner_And_Outer_Joins_Mixed()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c1 = new TestCustomer(uow) { Name = "X1", Email = "x1" };
                var c2 = new TestCustomer(uow) { Name = "X2", Email = "x2" };

                new TestOrder(uow) { Customer = c1, ProductName = "A", Total = 100, Quantity = 1 };
                new TestOrder(uow) { Customer = c1, ProductName = "B", Total = 200, Quantity = 2 };
                new TestOrder(uow) { Customer = c2, ProductName = "C", Total = 300, Quantity = 3 };

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                // test mix of filtering + multiple join chains
                var coll = new XPCollection<TestOrder>(
                    uow,
                    CriteriaOperator.Parse("Customer.Name in ('X1','X2') AND Quantity >= 2"));

                var rows = coll.Select(o => new
                {
                    o.Customer.Name,
                    o.ProductName
                }).OrderBy(x => x.ProductName).ToList();

                rows.Should().HaveCount(2);
                rows.Select(r => r.ProductName).Should().Equal("B", "C");
            }
        }
    }
}
