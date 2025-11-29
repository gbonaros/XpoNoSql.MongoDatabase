using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;


[Collection(XpoCollection.Name)]
public class BetweenAndNotLikeTests
{
    private readonly DbFixture _fx;
    public BetweenAndNotLikeTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Between_Inclusive_Range()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Metric(uow) { A = 1, B = 0, Flags = 0 };
        new Metric(uow) { A = 5, B = 0, Flags = 0 };
        new Metric(uow) { A = 10, B = 0, Flags = 0 };
        new Metric(uow) { A = 11, B = 0, Flags = 0 };
        uow.CommitChanges();

        var crit = CriteriaOperator.Parse("[A] Between (5, 10)"); // inclusive
        var res = new XPCollection<Metric>(uow, crit).Select(m => m.A).OrderBy(x => x).ToList();

        res.Should().Equal(new[] { 5, 10 });
    }
    ////[Fact]
    ////public void NotLike_And_Not_Like_AreEquivalent()
    ////{
    ////    using var uow = _fx.NewUow();
    ////    Cleanup(uow);

    ////    new Person(uow) { Name = "Alpha" };
    ////    new Person(uow) { Name = "Beta" };
    ////    new Person(uow) { Name = "Gamma" };
    ////    new Person(uow) { Name = "Delta" };
    ////    uow.CommitChanges();

    ////    var a = new XPCollection<Person>(uow, CriteriaOperator.Parse("Name Not Like 'A%'"))
    ////        .Select(p => p.Name).OrderBy(x => x).ToList();
    ////    var b = new XPCollection<Person>(uow, CriteriaOperator.Parse("Not (Name Like 'A%')"))
    ////        .Select(p => p.Name).OrderBy(x => x).ToList();

    ////    a.Should().Equal(new[] { "Beta", "Delta", "Gamma" });
    ////    b.Should().Equal(a);
    ////}

    //[Fact]
    //public void NotLike_ExcludesPattern()
    //{
    //    using var uow = _fx.NewUow();
    //    Cleanup(uow);

    //    new Person(uow) { Name = "Alpha" };
    //    new Person(uow) { Name = "Beta" };
    //    new Person(uow) { Name = "Gamma" };
    //    new Person(uow) { Name = "Delta" };
    //    uow.CommitChanges();

    //    var crit = CriteriaOperator.Parse("Name Not Like 'A%'");
    //    var res = new XPCollection<Person>(uow, crit).Select(p => p.Name).OrderBy(x => x).ToList();

    //    res.Should().Equal(new[] { "Beta", "Delta", "Gamma" });
    //}

    [Fact]
    public void In_WithManyItems_Works()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var names = new[] { "N1", "N2", "N3", "N4", "N5", "N6", "N7", "N8", "N9", "N10", "N11", "N12" };
        foreach (var n in names) new Person(uow) { Name = n };
        uow.CommitChanges();

        var crit = CriteriaOperator.Parse("Name In ('N3','N5','N11','N12')");
        var res = new XPCollection<Person>(uow, crit).Select(p => p.Name).OrderBy(x => x).ToList();

        res.Should().Equal(new[] { "N11", "N12", "N3", "N5" });
    }

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
