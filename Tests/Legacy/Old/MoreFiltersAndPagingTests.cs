using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class MoreFiltersAndPagingTests
{
    private readonly DbFixture _fx;
    public MoreFiltersAndPagingTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Sorting_ThenSkipTop_YieldsDeterministicOrder()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var pAlpha = new Person(uow) { Name = "Alpha" };
        var pBeta = new Person(uow) { Name = "Beta" };
        var pGamma = new Person(uow) { Name = "Gamma" };
        var pDelta = new Person(uow) { Name = "Delta" };
        uow.CommitChanges();

        var allSorted = new XPCollection<Person>(uow)
        {
            Sorting = { new SortProperty(nameof(Person.Name), SortingDirection.Ascending) }
        }.Select(p => p.Name).ToList();

        // Lexicographic: Alpha, Beta, Delta, Gamma
        allSorted.Should().Equal(new[] { "Alpha", "Beta", "Delta", "Gamma" });

        var paged = new XPCollection<Person>(uow)
        {
            Sorting = { new SortProperty(nameof(Person.Name), SortingDirection.Ascending) },
            SkipReturnedObjects = 1,
            TopReturnedObjects = 2
        }.Select(p => p.Name).ToList();

        paged.Should().Equal(new[] { "Beta", "Delta" });
    }

    //[Fact]
    //public void BooleanCombos_Not_And_Or_OnNames()
    //{
    //    using var uow = _fx.NewUow();
    //    Cleanup(uow);

    //    new Person(uow) { Name = "Alpha" };
    //    new Person(uow) { Name = "Beta" };
    //    new Person(uow) { Name = "Gamma" };
    //    new Person(uow) { Name = "Delta" };
    //    uow.CommitChanges();

    //    // NOT (Name Like 'Al%') AND (Name Like '%a%')
    //    var crit = new GroupOperator(GroupOperatorType.And,
    //        new UnaryOperator(UnaryOperatorType.Not,
    //            CriteriaOperator.Parse("Name Like 'Al%'")),
    //        CriteriaOperator.Parse("Name Like '%a%'"));

    //    var result = new XPCollection<Person>(uow, crit).Select(p => p.Name).OrderBy(x => x).ToList();
    //    // Expect Beta, Delta, Gamma (Alpha is excluded by NOT 'Al%')
    //    result.Should().Equal(new[] { "Beta", "Delta", "Gamma" });
    //}

    //[Fact]
    //public void EmptyResult_SimpleFilterReturnsNone()
    //{
    //    using var uow = _fx.NewUow();
    //    Cleanup(uow);

    //    new Person(uow) { Name = "Alpha" };
    //    new Person(uow) { Name = "Beta" };
    //    uow.CommitChanges();

    //    var none = new XPCollection<Person>(uow, CriteriaOperator.Parse("Name Like 'Z%'")).ToList();
    //    none.Should().BeEmpty();
    //}

    private static void Cleanup(UnitOfWork uow)
    {
        foreach (var obj in new XPCollection<UserRole>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<Kid>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<AppUser>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<AppRole>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<Person>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<Metric>(uow).ToList()) obj.Delete();
        uow.CommitChanges();
    }
}
