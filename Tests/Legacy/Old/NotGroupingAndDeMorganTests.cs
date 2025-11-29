using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class NotGroupingAndDeMorganTests
{
    private readonly DbFixture _fx;
    public NotGroupingAndDeMorganTests(DbFixture fx) => _fx = fx;

    //[Fact]
    //public void Not_Group_With_Or_Equivalent_To_DeMorgan_And()
    //{
    //    using var uow = _fx.NewUow();
    //    Cleanup(uow);

    //    new Person(uow) { Name = "Alpha" };
    //    new Person(uow) { Name = "Beta" };
    //    new Person(uow) { Name = "Gamma" };
    //    new Person(uow) { Name = "Delta" };
    //    uow.CommitChanges();

    //    // A: Not (Name Like 'A%' Or Name Like 'G%')
    //    var a = new XPCollection<Person>(uow,
    //        CriteriaOperator.Parse("Not ([Name] Like 'A%' Or [Name] Like 'G%')"))
    //        .Select(p => p.Name).OrderBy(x => x).ToList();

    //    // B: (Not Name Like 'A%') And (Not Name Like 'G%')
    //    var b = new XPCollection<Person>(uow,
    //        CriteriaOperator.Parse("(Not ([Name] Like 'A%')) And (Not ([Name] Like 'G%'))"))
    //        .Select(p => p.Name).OrderBy(x => x).ToList();

    //    a.Should().Equal(new[] { "Beta", "Delta" });
    //    b.Should().Equal(a); // De Morgan equivalence
    //}

    [Fact]
    public void Not_On_Equality_Works_As_Ne()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Person(uow) { Name = "Alpha" };
        new Person(uow) { Name = "Beta" };
        new Person(uow) { Name = "Gamma" };
        uow.CommitChanges();

        var notEq = new XPCollection<Person>(uow, CriteriaOperator.Parse("Not ([Name] = 'Alpha')"))
            .Select(p => p.Name).OrderBy(x => x).ToList();

        notEq.Should().Equal(new[] { "Beta", "Gamma" });
    }

    //[Fact]
    //public void Mixed_Not_With_Or_And()
    //{
    //    using var uow = _fx.NewUow();
    //    Cleanup(uow);

    //    new Person(uow) { Name = "Alpha" };
    //    new Person(uow) { Name = "Beta" };
    //    new Person(uow) { Name = "Gamma" };
    //    new Person(uow) { Name = "Delta" };
    //    uow.CommitChanges();

    //    // (Not Name Like 'A%') And (Name Like '%a%')
    //    var crit = CriteriaOperator.Parse("(Not ([Name] Like 'A%')) And ([Name] Like '%a%')");
    //    var res = new XPCollection<Person>(uow, crit).Select(p => p.Name).OrderBy(x => x).ToList();

    //    // should include 'Beta', 'Delta', 'Gamma' that contain 'a' but not start with 'A'
    //    res.Should().Equal(new[] { "Beta", "Delta", "Gamma" });
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
