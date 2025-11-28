using DevExpress.Xpo;

using FluentAssertions;

using System;
using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests
{
    [Collection(XpoCollection.Name)]
    public sealed class MongoLinqProjectionAndDistinctTests
    {
        private readonly DbFixture fixture;

        public MongoLinqProjectionAndDistinctTests(DbFixture fixture)
        {
            this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        [Fact]
        public void Distinct_Customers_From_Orders_With_Ordering()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();
            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var a = new TestCustomer(uow) { Name = "Alice", Email = "da@example.com" };
                var b = new TestCustomer(uow) { Name = "Bob", Email = "db@example.com" };

                new TestOrder(uow) { Customer = a, ProductName = "P1", Quantity = 1, Total = 10m };
                new TestOrder(uow) { Customer = a, ProductName = "P2", Quantity = 2, Total = 20m };
                new TestOrder(uow) { Customer = b, ProductName = "P3", Quantity = 3, Total = 30m };
                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var customers = uow.Query<TestOrder>()
                    .Where(o => o.Customer != null)
                    .Select(o => new { Cust = o.Customer, o.Customer!.Name })
                    .Distinct()
                    .OrderBy(c => c.Name)
                    .Select(c => c.Cust)
                    .ToArray();

                customers.Should().HaveCount(2);
                customers[0]!.Name.Should().Be("Alice");
                customers[1]!.Name.Should().Be("Bob");
            }
        }

        [Fact]
        public void Projection_To_Anonymous_Type_With_Join()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();
            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c1 = new TestCustomer(uow) { Name = "Proj-A", Email = "pa@example.com" };
                var c2 = new TestCustomer(uow) { Name = "Proj-B", Email = "pb@example.com" };

                new TestOrder(uow) { Customer = c1, ProductName = "P1", Quantity = 1, Total = 10m };
                new TestOrder(uow) { Customer = c2, ProductName = "P2", Quantity = 2, Total = 20m };
                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var rows = uow.Query<TestOrder>()
                    .Where(o => o.Customer != null)
                    .OrderByDescending(o => o.Total)
                    .Select(o => new
                    {
                        o.ProductName,
                        o.Total,
                        CustomerName = o.Customer!.Name
                    })
                    .ToList();

                rows.Should().HaveCount(2);
                rows[0].Total.Should().Be(20m);
                rows[0].CustomerName.Should().Be("Proj-B");
                rows[1].Total.Should().Be(10m);
                rows[1].CustomerName.Should().Be("Proj-A");
            }
        }
    }
}
