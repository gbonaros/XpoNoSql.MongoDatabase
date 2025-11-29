using DevExpress.Xpo;

using FluentAssertions;

using System;
using System.Linq;

using Xunit;

namespace XpoNoSql.Tests
{
    [Collection(XpoCollection.Name)]
    public sealed class NightmareInheritancePackTests
    {
        private readonly DbFixture fixture;

        public NightmareInheritancePackTests(DbFixture fixture)
        {
            this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        // ────────────────────────────────────────────────────────────────
        // 1) TypeOf(This) tests
        // ────────────────────────────────────────────────────────────────
        [Fact]
        public void TypeOf_Filter_On_Derived_Class()
        {
            fixture.Cleanup<NP3_PersonBase>();
            fixture.Cleanup<NP3_Employee>();
            fixture.Cleanup<NP3_Manager>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                new NP3_PersonBase(uow) { FullName = "Base_1" };
                new NP3_Employee(uow) { FullName = "Emp_1", Salary = 1000 };
                new NP3_Manager(uow) { FullName = "Mgr_1", Salary = 2000, Bonus = 300 };

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var q = new XPQuery<NP3_PersonBase>(uow)
                    .Where(p => p is NP3_Manager)
                    .Select(p => p.FullName)
                    .ToList();

                q.Should().Equal("Mgr_1");
            }
        }

        // ────────────────────────────────────────────────────────────────
        // 2) Inheritance + navigation
        // ────────────────────────────────────────────────────────────────
        [Fact]
        public void Manager_Employee_Navigation_And_OrderQuery()
        {
            fixture.Cleanup<NP3_PersonBase>();
            fixture.Cleanup<NP3_Employee>();
            fixture.Cleanup<NP3_Manager>();
            fixture.Cleanup<TestOrder>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var m = new NP3_Manager(uow)
                {
                    FullName = "Manager_A",
                    Salary = 1000,
                    Bonus = 200
                };

                var e1 = new NP3_Employee(uow)
                {
                    FullName = "Emp_A",
                    Manager = m,
                    Salary = 500
                };

                var e2 = new NP3_Employee(uow)
                {
                    FullName = "Emp_B",
                    Manager = m,
                    Salary = 700
                };

                // Orders assigned to employees
                new TestOrder(uow) { Customer = null, ProductName = "X", Quantity = 1, Total = 10m };
                new TestOrder(uow) { Customer = null, ProductName = "Y", Quantity = 1, Total = 20m };

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                // Query all employees under managers whose bonus >= 100
                var q = new XPQuery<NP3_Employee>(uow)
                    .Where(e => e.Manager != null && e.Manager.Bonus >= 100)
                    .OrderBy(e => e.FullName)
                    .Select(e => new
                    {
                        e.FullName,
                        Mgr = e.Manager.FullName
                    })
                    .ToList();

                q.Should().HaveCount(2);
                q.Should().Contain(x => x.FullName == "Emp_A" && x.Mgr == "Manager_A");
                q.Should().Contain(x => x.FullName == "Emp_B" && x.Mgr == "Manager_A");
            }
        }

        // ────────────────────────────────────────────────────────────────
        // 3) Mixed hierarchy grouping
        // ────────────────────────────────────────────────────────────────
        [Fact]
        public void GroupBy_Polymorphic_With_Aggregates()
        {
            fixture.Cleanup<NP3_PersonBase>();
            fixture.Cleanup<NP3_Employee>();
            fixture.Cleanup<NP3_Manager>();

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                new NP3_Employee(uow) { FullName = "Emp_A", Salary = 100 };
                new NP3_Employee(uow) { FullName = "Emp_B", Salary = 200 };
                new NP3_Manager(uow) { FullName = "Mgr_A", Salary = 500, Bonus = 100 };
                new NP3_Manager(uow) { FullName = "Mgr_B", Salary = 600, Bonus = 200 };

                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var q =
                    new XPQuery<NP3_PersonBase>(uow)
                    .GroupBy(p => p is NP3_Manager)
                    .Select(g => new
                    {
                        IsManager = g.Key,
                        Count = g.Count(),
                        AvgSalary = g.Average(p => p.Salary)
                    })
                    // order managers after employees just to force a deterministic order
                    .OrderBy(x => x.IsManager ? 1 : 0)
                    .ToList();

                q.Should().HaveCount(2);

                var emp = q.Single(x => x.IsManager == false);
                var mgr = q.Single(x => x.IsManager == true);

                emp.Count.Should().Be(2);
                emp.AvgSalary.Should().Be(150);

                mgr.Count.Should().Be(2);
                mgr.AvgSalary.Should().Be(550);
            }
        }
    }
}
