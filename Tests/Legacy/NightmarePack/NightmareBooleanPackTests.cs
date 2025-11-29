using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System;
using System.Linq;

using Xunit;

namespace XpoNoSql.Tests
{
    [Collection(XpoCollection.Name)]
    public sealed class NightmareBooleanPackTests
    {
        private readonly DbFixture fixture;

        public NightmareBooleanPackTests(DbFixture fixture)
        {
            this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        [Fact]
        public void Complex_Boolean_Criteria_With_Not_And_Or_Groups()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c = new TestCustomer(uow) { Name = "Bool_C", Email = "bool@x.com" };

                // Will test boolean logic:
                // (NOT (Total >= 50) AND Quantity >= 5) OR (Total >= 100 AND NOT (Quantity < 10))
                new TestOrder(uow) { Customer = c, ProductName = "T1", Quantity = 4, Total = 40m };   // exclude
                new TestOrder(uow) { Customer = c, ProductName = "T2", Quantity = 6, Total = 40m };   // include (left branch)
                new TestOrder(uow) { Customer = c, ProductName = "T3", Quantity = 5, Total = 60m };   // exclude
                new TestOrder(uow) { Customer = c, ProductName = "T4", Quantity = 15, Total = 120m };  // include (right branch)
                new TestOrder(uow) { Customer = c, ProductName = "T5", Quantity = 8, Total = 100m };  // exclude (Quantity < 10)

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var criteria = CriteriaOperator.Parse(
                    "(Not ([Total] >= 50) And [Quantity] >= 5) " +
                    "Or ([Total] >= 100 And Not ([Quantity] < 10))");

                var coll = new XPCollection<TestOrder>(uow, criteria);
                var names = coll.Select(o => o.ProductName).OrderBy(x => x).ToList();

                // T2 (40, qty 6) matches left side
                // T4 (120, qty 15) matches right side
                names.Should().Equal("T2", "T4");
            }
        }

        [Fact]
        public void Double_Negation_And_Mixed_Groups_Behave_As_Expected()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var c = new TestCustomer(uow) { Name = "Bool_DN", Email = "dn@x.com" };

                // We'll filter on Description-like pattern with double NOT logic via criteria on ProductName.
                new TestOrder(uow) { Customer = c, ProductName = "AX_1", Quantity = 1, Total = 10m };
                new TestOrder(uow) { Customer = c, ProductName = "BX_2", Quantity = 1, Total = 20m };
                new TestOrder(uow) { Customer = c, ProductName = "AY_3", Quantity = 1, Total = 30m };
                new TestOrder(uow) { Customer = c, ProductName = "CZ_4", Quantity = 1, Total = 40m };

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                // Equivalent to:
                // NOT (Name starts with 'C' OR Name contains 'Y') AND NOT (Name starts with 'B')
                // -> Only AX_1 survives
                var criteria = CriteriaOperator.Parse(
                    "Not (StartsWith([ProductName], 'C') Or Contains([ProductName], 'Y')) " +
                    "And Not (StartsWith([ProductName], 'B'))");

                var coll = new XPCollection<TestOrder>(uow, criteria);
                var names = coll.Select(o => o.ProductName).OrderBy(x => x).ToList();

                names.Should().Equal("AX_1");
            }
        }
    }
}
