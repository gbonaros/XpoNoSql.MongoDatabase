using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;

using FluentAssertions;

using System;
using System.Linq;

using Xunit;

namespace XpoNoSql.Tests
{
    [Collection(XpoCollection.Name)]
    public sealed class MongoJoinAndFilterTests
    {
        private readonly DbFixture fixture;

        public MongoJoinAndFilterTests(DbFixture fixture)
        {
            this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        [Fact]
        public void XPQuery_Filter_By_Navigation_Properties()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();
            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c1 = new TestCustomer(uow) { Name = "Alice", Email = "alice@example.com" };
                var c2 = new TestCustomer(uow) { Name = "Bob", Email = "bob@example.com" };

                new TestOrder(uow)
                {
                    Customer = c1,
                    ProductName = "P1",
                    Quantity = 1,
                    Total = 10m
                };
                new TestOrder(uow)
                {
                    Customer = c1,
                    ProductName = "P2",
                    Quantity = 5,
                    Total = 50m
                };
                new TestOrder(uow)
                {
                    Customer = c2,
                    ProductName = "P3",
                    Quantity = 3,
                    Total = 30m
                };

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var q = new XPQuery<TestOrder>(uow)
                    .Where(o => o.Customer != null &&
                                o.Customer.Name == "Alice" &&
                                o.Quantity >= 2)
                    .OrderBy(o => o.ProductName)
                    .ToList();

                q.Should().HaveCount(1);
                q[0].ProductName.Should().Be("P2");
                q[0].Customer!.Name.Should().Be("Alice");
            }
        }

        [Fact]
        public void XPCollection_Criteria_On_Nested_Path()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();
            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c1 = new TestCustomer(uow) { Name = "Nested1", Email = "n1@example.com" };
                var c2 = new TestCustomer(uow) { Name = "Nested2", Email = "n2@example.com" };

                new TestOrder(uow)
                {
                    Customer = c1,
                    ProductName = "N-P1",
                    Quantity = 1,
                    Total = 10m
                };
                new TestOrder(uow)
                {
                    Customer = c2,
                    ProductName = "N-P2",
                    Quantity = 2,
                    Total = 20m
                };
                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var criteria = CriteriaOperator.Parse("Customer.Name = ? OR Customer.Email = ?", "Nested2", "n1@example.com");
                var sort = new[]
                {
                    new SortProperty(nameof(TestOrder.Total), SortingDirection.Ascending)
                };

                var coll = new XPCollection<TestOrder>(uow, criteria, sort);

                var rows = coll.ToList();
                rows.Should().HaveCount(2);
                rows.Select(r => r.Customer!.Name).OrderBy(n => n)
                    .Should().Equal("Nested1", "Nested2");
            }
        }
    }
}
