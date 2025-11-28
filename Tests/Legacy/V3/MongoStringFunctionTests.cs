using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System;
using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests
{
    [Collection(XpoCollection.Name)]
    public sealed class MongoStringFunctionTests
    {
        private readonly DbFixture fixture;

        public MongoStringFunctionTests(DbFixture fixture)
        {
            this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        [Fact]
        public void Contains_And_StartsWith_Are_CaseInsensitive()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();
            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                new TestCustomer(uow) { Name = "alpha", Email = "a1@example.com" };
                new TestCustomer(uow) { Name = "Bravo", Email = "b1@example.com" };
                new TestCustomer(uow) { Name = "CHARLIE", Email = "c1@example.com" };
                uow.CommitChanges();
            }

            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                var q = new XPQuery<TestCustomer>(uow)
                    .Where(c => c.Name.Contains("a"))
                    .OrderBy(c => c.Name)
                    .ToList();

                // Current collation/sort returns case-sensitive ordering.
                q.Select(c => c.Name).Should().Equal("alpha", "Bravo", "CHARLIE");

                var starts = new XPQuery<TestCustomer>(uow)
                    .Where(c => c.Name.StartsWith("ch"))
                    .ToList();

                starts.Should().HaveCount(1);
                starts[0].Name.Should().Be("CHARLIE");
            }
        }

        [Fact]
        public void Trim_And_Len_Functions_In_XPView()
        {
            fixture.Cleanup<TestCustomer>();
            fixture.Cleanup<TestOrder>();
            using (var uow = new UnitOfWork(fixture.DataLayer))
            {
                new TestCustomer(uow) { Name = "  Alpha  ", Email = "t1@example.com" };
                new TestCustomer(uow) { Name = "Beta", Email = "t2@example.com" };
                new TestCustomer(uow) { Name = " Gamma", Email = "t3@example.com" };
                uow.CommitChanges();

                var view = new XPView(uow, typeof(TestCustomer));
                view.Properties.Add(new ViewProperty("Trimmed", SortDirection.Ascending, "Trim([Name])", false, true));
                view.Properties.Add(new ViewProperty("Len", SortDirection.None, "Len([Name])", false, true));

                var rows = view.Cast<ViewRecord>()
                    .Select(r => new { T = (string)r["Trimmed"], L = (int)r["Len"] })
                    .OrderBy(x => x.T)
                    .ToList();

                rows.Select(x => x.T).Should().Equal("Alpha", "Beta", "Gamma");
                rows.Single(x => x.T == "Alpha").L.Should().Be(7);
            }
        }

        //[Fact]
        //public void Criteria_With_Like_And_Custom_Function()
        //{
        //    fixture.Cleanup<TestCustomer>();
        //    fixture.Cleanup<TestOrder>();
        //    using (var uow = new UnitOfWork(fixture.DataLayer))
        //    {
        //        new TestCustomer(uow) { Name = "LikeA", Email = "l1@example.com" };
        //        new TestCustomer(uow) { Name = "LikeB", Email = "l2@example.com" };
        //        new TestCustomer(uow) { Name = "Other", Email = "l3@example.com" };
        //        uow.CommitChanges();
        //    }

        //    using (var uow = new UnitOfWork(fixture.DataLayer))
        //    {
        //        var criteria = CriteriaOperator.Parse("[Name] Like 'Like%'");
        //        var coll = new XPCollection<TestCustomer>(uow, criteria);

        //        var names = coll.Select(c => c.Name).OrderBy(n => n).ToList();
        //        names.Should().Equal("LikeA", "LikeB");
        //    }
        //}
    }
}
