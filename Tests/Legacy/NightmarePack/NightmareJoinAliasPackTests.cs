using DevExpress.Xpo;

using FluentAssertions;

using System;
using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests
{
    [Collection(XpoCollection.Name)]
    public sealed class NightmareJoinAliasPackTests
    {
        private readonly DbFixture fixture;

        public NightmareJoinAliasPackTests(DbFixture fixture)
        {
            this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        [Fact]
        public void SelfJoin_On_Same_Table_By_Email()
        {
            fixture.Cleanup<TestCustomer>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c1 = new TestCustomer(uow) { Name = "Self_A", Email = "foo@domain.com" };
                var c2 = new TestCustomer(uow) { Name = "Self_B", Email = "foo@domain.com" };
                var c3 = new TestCustomer(uow) { Name = "Self_C", Email = "bar@domain.com" };
                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var q =
                    from c1 in new XPQuery<TestCustomer>(uow)
                    join c2 in new XPQuery<TestCustomer>(uow)
                        on c1.Email equals c2.Email
                    where c1.Oid < c2.Oid
                    orderby c1.Name, c2.Name
                    select new
                    {
                        LeftName = c1.Name,
                        RightName = c2.Name,
                        Email = c1.Email
                    };

                var rows = q.ToList();

                rows.Should().HaveCount(1);
                rows[0].LeftName.Should().Be("Self_A");
                rows[0].RightName.Should().Be("Self_B");
                rows[0].Email.Should().Be("foo@domain.com");
            }
        }

        [Fact]
        public void SelfJoin_On_Orders_By_Customer_And_Total_Comparison()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c = new TestCustomer(uow) { Name = "Join_C", Email = "join@x.com" };

                new TestOrder(uow) { Customer = c, ProductName = "Low", Quantity = 1, Total = 10m };
                new TestOrder(uow) { Customer = c, ProductName = "Mid", Quantity = 1, Total = 20m };
                new TestOrder(uow) { Customer = c, ProductName = "High", Quantity = 1, Total = 30m };

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                // Pairs (o1,o2) where same customer and o1.Total < o2.Total
                var q =
                    from o1 in new XPQuery<TestOrder>(uow)
                    join o2 in new XPQuery<TestOrder>(uow)
                        on o1.Customer.Oid equals o2.Customer.Oid
                    where o1.Total < o2.Total
                    orderby o1.Total, o2.Total
                    select new
                    {
                        Left = o1.ProductName,
                        Right = o2.ProductName,
                        Customer = o1.Customer.Name
                    };

                var rows = q.ToList();

                // Expected pairs:
                // (Low,Mid), (Low,High), (Mid,High)
                rows.Should().HaveCount(3);
                rows.Select(r => (r.Left, r.Right)).Should().BeEquivalentTo(new[]
                {
                    ("Low", "Mid"),
                    ("Low", "High"),
                    ("Mid", "High")
                });
                rows.Select(r => r.Customer).Distinct().Single().Should().Be("Join_C");
            }
        }
    }
}
